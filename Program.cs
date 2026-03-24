using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using dotenv.net;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;


var builder = WebApplication.CreateBuilder(args);

/* =====================================================
 * 1. REGISTER SERVICES (TRƯỚC Build)
 * ===================================================== */

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// azdot env load
//builder.WebHost.UseUrls("http://+:8080");

// Đăng ký CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
            ;
    });
});


// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

// HttpContext
builder.Services.AddHttpContextAccessor();

// SignalR
builder.Services.AddSignalR();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("KpettChatAppDb")));
// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new InvalidOperationException("Redis connection string is not configured.");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

    var configuration = ConfigurationOptions.Parse(redisConnectionString);

    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;

    return ConnectionMultiplexer.Connect(configuration);
});

// Set my Cloudinary credentials
var account = new Account(
    builder.Configuration["CloudinarySettings:CloudName"],
    builder.Configuration["CloudinarySettings:ApiKey"],
    builder.Configuration["CloudinarySettings:ApiSecret"]);
var cloudinary = new Cloudinary(account);

// JWT Authentication
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

                var jtiClaim = context.Principal?.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti);

                if (jtiClaim != null)
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

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chat-Hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },

            // Custom Response khi lỗi 401 (Không có token, sai token, hoặc bị Fail() ở OnTokenValidated)
            OnChallenge = async context =>
            {
                // Vô hiệu hóa cơ chế sinh lỗi mặc định của ASP.NET
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                // Message mặc định
                string errorMessage = "Token invalid";

                // Bắt chính xác lỗi để trả về thông báo phù hợp cho client
                if (context.AuthenticateFailure != null)
                {
                    // Lỗi do token hết hạn
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

            // Custom Response khi lỗi 403 (Có token hợp lệ nhưng không đủ quyền Role/Policy)
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var errorResponse = new ErrorResponse
                {
                    IsSuccess = false,
                    StatusCode = 401,
                    ErrorCode = ErrorCodes.AUTH.FORBIDDEN,
                    Message = "You do not have permission to access this resource."
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
        };
    });

// Authorization
builder.Services.AddAuthorization();

// Application Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddSingleton(cloudinary);
builder.Services.AddScoped<ICloudinaryService, UploadFileService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IRealtimeService, RealtimeService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFriendshipService, FriendshipServices>();
builder.Services.AddScoped<IPostFeedService, PostFeedService>();

// Global Exception Handler (ĐĂNG KÝ Ở ĐÂY)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

/* =====================================================
 * 2. BUILD APP (SAU KHI Add xong)
 * ===================================================== */

var app = builder.Build();

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
/* =====================================================
 * 3. MIDDLEWARE PIPELINE
 * ===================================================== */

// Global Exception
app.UseExceptionHandler();

// HTTPS
app.UseHttpsRedirection();

// Routing
app.UseRouting();

app.UseCors("ClientCors");

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Token blacklist validation (after auth, before endpoints)
app.UseTokenBlacklistMiddleware();

// OpenAPI (DEV only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints
app.MapControllers();
app.MapHub<ChatHub>("/chat-Hub").RequireCors("ClientCors");

// Test exception
//app.MapGet("/", () => { throw new Exception("Test error"); });
//app.MapGet("/health", () => "OK");


app.Run();