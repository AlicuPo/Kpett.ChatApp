using CloudinaryDotNet;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Middlewares;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Impls;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("KpettChatAppDb")));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new InvalidOperationException("Redis connection string is not configured.");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

var account = new Account(
    builder.Configuration["CloudinarySettings:CloudName"],
    builder.Configuration["CloudinarySettings:ApiKey"],
    builder.Configuration["CloudinarySettings:ApiSecret"]);
builder.Services.AddSingleton(new Cloudinary(account));

var jwtSection = builder.Configuration.GetSection("JwtSection");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var keyAccess = jwtSection["KeyAccess"];

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyAccess!)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var redis = context.HttpContext.RequestServices.GetRequiredService<IRedisService>();
                var jti = context.Principal!.Claims.First(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

                if (await redis.IsAccessTokenBlacklistedAsync(jti))
                {
                    context.Fail("Token revoked");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    ErrorCode = ErrorCodes.AUTH.UNAUTHORIZED,
                    Message = "Unauthorized"
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    ErrorCode = ErrorCodes.AUTH.FORBIDDEN,
                    Message = "Forbidden"
                });
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chat-Hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<ICloudinaryService, UploadFileService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IConversationAccessService, ConversationAccessService>();
builder.Services.AddScoped<IConversationPresenceService, ConversationPresenceService>();
builder.Services.AddScoped<IRealtimeService, RealtimeService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFriendshipService, FriendshipServices>();
builder.Services.AddScoped<IPostFeedService, PostFeedService>();

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
app.UseTokenBlacklistMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<ChatHub>("/chat-Hub").RequireCors("ClientCors");

app.Run();

public partial class Program
{
}
