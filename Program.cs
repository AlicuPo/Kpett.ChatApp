using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using dotenv.net;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Reposoitory;
using Kpett.ChatApp.Respository;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

/* =====================================================
 * 1. REGISTER SERVICES (TRƯỚC Build)
 * ===================================================== */

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// azdot env load
//builder.WebHost.UseUrls("http://+:8080");


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
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    // Cho phép kết nối lại khi Redis sẵn sàng
    configuration.AbortOnConnectFail = false;
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
                var redis = context.HttpContext.RequestServices.GetRequiredService<Kpett.ChatApp.Services.IRedis>();

                var jti = context.Principal!
                    .Claims.First(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

                if (await redis.IsAccessTokenBlacklistedAsync(jti))
                {
                    context.Fail("Token revoked");
                }
            },

            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/chat-Hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// Authorization
builder.Services.AddAuthorization();

// Application Services
builder.Services.AddScoped<IToken, TokenRespository>();
builder.Services.AddScoped<ILogin, LoginRespository>();
builder.Services.AddScoped<Kpett.ChatApp.Services.IRedis, RedisRespository>();
builder.Services.AddSingleton(cloudinary);
builder.Services.AddScoped<Kpett.ChatApp.Services.ICloudinary, UploadFileRepository>();
builder.Services.AddScoped<IMessage, MessageRespository>();
builder.Services.AddScoped<IConversation, ConversationImpl>();
builder.Services.AddScoped<IRealtimeService, RealtimeRespository>();
builder.Services.AddScoped<INotificationService, NotificationRespository>();
builder.Services.AddScoped<IUsers, UserRespository>();
builder.Services.AddScoped<IFriendshipsService, FriendshipsServicesImpl>();
builder.Services.AddScoped<IPostFeedService, PostFeedServiceImpl>();

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
app.MapHub<ChatHub>("/chat-Hub");

// Test exception
app.MapGet("/", () =>
{
    throw new Exception("Test error");
});
//app.MapGet("/health", () => "OK");


app.Run();
