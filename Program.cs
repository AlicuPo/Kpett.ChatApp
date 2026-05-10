using CloudinaryDotNet;
using Hangfire;
using Kpett.ChatApp.Configs;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Kpett.ChatApp.Services.Impls;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Kpett.ChatApp.Be.Services.Impls;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();

//Configure DbContext with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("KpettChatAppDb")));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new InvalidOperationException("Redis connection string is not configured.");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    // Cho phÃ©p káº¿t ná»‘i láº¡i khi Redis sáºµn sÃ ng
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;

    return ConnectionMultiplexer.Connect(configuration);
});

// Configure Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("KpettChatAppDb")));

// Add the Hangfire server to process background jobs
builder.Services.AddHangfireServer();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

var account = new Account(
    builder.Configuration["CloudinarySettings:CloudName"],
    builder.Configuration["CloudinarySettings:ApiKey"],
    builder.Configuration["CloudinarySettings:ApiSecret"]);
builder.Services.AddSingleton(new Cloudinary(account));

var jwtSection = builder.Configuration.GetSection("JwtSection");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var KeyAccess = jwtSection["KeyAccess"];


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(KeyAccess!)
            ),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var redis = context.HttpContext.RequestServices
                    .GetRequiredService<Kpett.ChatApp.Services.Interfaces.IRedisService>();

                var jtiClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti);

                if (jtiClaim != null && !string.IsNullOrEmpty(jtiClaim.Value))
                {
                    if (await redis.IsAccessTokenBlacklistedAsync(jtiClaim.Value))
                    {
                        context.Fail("Token đã bị thu hồi.");
                    }
                }
            },

            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs/app")))
                {
                    // Đọc token từ query string cho SignalR
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },

            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                string errorMessage = "Token invalid";

                if (context.AuthenticateFailure != null)
                {
                    if (context.AuthenticateFailure.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        errorMessage = "Token has Expired";
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                }

                var errorResponse = new ErrorResponse
                {
                    IsSuccess = false,
                    StatusCode = 401,
                    ErrorCode = ErrorCodes.AUTH.ACCESS_TOKEN_INVALID,
                    Message = errorMessage
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var errorResponse = new ErrorResponse
                {
                    IsSuccess = false,
                    StatusCode = 403,
                    ErrorCode = ErrorCodes.AUTH.FORBIDDEN,
                    Message = "You do not have permission to access this resource."
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
        };
    });

builder.Services.AddAuthorization();

// Options pattern
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSection"));
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection("MediaSettings"));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<ICloudinaryService, UploadFileService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IConversationAccessService, ConversationAccessService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRelationshipService, RelationshipService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IConversationAccessService, ConversationAccessService>();
builder.Services.AddScoped<IConversationTypingService, ConversationTypingService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("ClientCors");
app.UseAuthentication();
app.UseAuthorization();

// Enable Hangfire Dashboard (optional, for monitoring background jobs)
app.UseHangfireDashboard();

// Schedule a recurring job to clean up orphaned images daily at 2 AM
RecurringJob.AddOrUpdate<IMediaService>(
    "cleanup-temp-images",
    service => service.CleanUpOrphanedImagesAsync(),
    Cron.Daily(2)
);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<AppHub>("/hubs/app").RequireCors("ClientCors");

app.Run();


