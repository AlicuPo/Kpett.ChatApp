using CloudinaryDotNet;
using Kpett.ChatApp.Configs;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Middlewares;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
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
using System.Text.Json;

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
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    // Cho phép kết nối lại khi Redis sẵn sàng
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;

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
            ValidIssuer = issuer, // Đảm bảo biến này đã được khai báo ở trên
            ValidAudience = audience, // Đảm bảo biến này đã được khai báo ở trên
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(KeyAccess!) // Đảm bảo KeyAccess đã được khai báo
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

                // Xử lý token cho kết nối WebSockets / SignalR
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chat-Hub"))
                {
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

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
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
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IConversationAccessService, ConversationAccessService>();
builder.Services.AddScoped<IConversationPresenceService, ConversationPresenceService>();
builder.Services.AddScoped<IRealtimeService, RealtimeService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRelationshipService, RelationshipService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IMediaService, MediaService>();

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
