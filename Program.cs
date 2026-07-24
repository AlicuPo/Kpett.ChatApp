using Hangfire;
using Kpett.ChatApp.Data;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Implementations;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsOptions>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientCors", policy =>
    {
        policy
            .WithOrigins(corsSettings?.AllowedOrigins ?? new[] { "http://localhost:3000", "http://localhost:52974" })
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

var sqlConnectionString = builder.Configuration.GetConnectionString("KpettChatAppDb")
    ?? throw new InvalidOperationException("ConnectionStrings:KpettChatAppDb is not configured.");

//Configure DbContext with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlConnectionString));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new InvalidOperationException("Redis connection string is not configured.");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

    var configuration = ConfigurationOptions.Parse(redisConnectionString);
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
    .UseSqlServerStorage(sqlConnectionString));

// Add the Hangfire server to process background jobs
builder.Services.AddHangfireServer();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

var jwtSection = builder.Configuration.GetSection("JwtSection");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var KeyAccess = jwtSection["KeyAccess"];

// Cấu hình Serilog đọc từ appsettings.json
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});


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
                    .GetRequiredService<Kpett.ChatApp.Services.Abstractions.IRedisService>();

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

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

// Options pattern
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSection"));
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection("MediaSettings"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IConversationAccessService, ConversationAccessService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRelationshipService, RelationshipService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IConversationTypingService, ConversationTypingService>();
builder.Services.AddScoped<IConversationMessageService, ConversationMessageService>();
builder.Services.AddScoped<IConversationMemberService, ConversationMemberService>();
builder.Services.AddScoped<IGroupsService, GroupsService>();
builder.Services.AddScoped<IGroupMemberService, GroupMemberService>();
builder.Services.AddScoped<IPostReactionService, PostReactionService>();
builder.Services.AddScoped<IStickerService, StickerService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("ClientCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

// Schedule a recurring job to clean up orphaned images daily at 2 AM
// Tạo một Service Scope để lấy các service từ DI Container
//using (var scope = app.Services.CreateScope())
//{
//    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

//    recurringJobManager.AddOrUpdate<IMediaService>(
//        "cleanup-temp-images",
//        service => service.CleanUpOrphanedImagesAsync(),
//        Cron.Daily(2)
//    );
//}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Expose Hangfire Dashboard only in development unless an authorization policy is added.
    app.UseHangfireDashboard();
}

// Apply pending EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.GetPendingMigrations().Any())
    {
        dbContext.Database.Migrate();
    }

    // Ensure Roles and UserRoles tables exist
    await dbContext.Database.ExecuteSqlRawAsync(
        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Roles') " +
        "CREATE TABLE Roles (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(256) NOT NULL, Description NVARCHAR(MAX) NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE())");
    await dbContext.Database.ExecuteSqlRawAsync(
        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='UserRoles') " +
        "CREATE TABLE UserRoles (UserId NVARCHAR(450) NOT NULL, RoleId INT NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), PRIMARY KEY (UserId, RoleId))");

    // Seed SuperAdmin role and default super admin user (chỉ khi cấu hình qua env/appsettings)
    var superAdminEmail = app.Configuration["SuperAdmin:Email"];
    var superAdminPassword = app.Configuration["SuperAdmin:Password"];

    if (!string.IsNullOrEmpty(superAdminEmail) && !string.IsNullOrEmpty(superAdminPassword))
    {
        var superAdminRoleName = "SuperAdmin";

        var role = await dbContext.Set<Kpett.ChatApp.Models.Role>().FirstOrDefaultAsync(r => r.Name == superAdminRoleName);
        if (role == null)
        {
            role = new Kpett.ChatApp.Models.Role
            {
                Name = superAdminRoleName,
                Description = "Super Administrator with full system access",
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Set<Kpett.ChatApp.Models.Role>().Add(role);
            await dbContext.SaveChangesAsync();
        }

        var adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == superAdminEmail);
        if (adminUser == null)
        {
            adminUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = superAdminEmail,
                Password = BCrypt.Net.BCrypt.HashPassword(superAdminPassword),
                Username = "admin",
                DisplayName = "Super Admin",
                IsActive = true,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);
            await dbContext.SaveChangesAsync();
        }

        var userRoleExists = await dbContext.Set<Kpett.ChatApp.Models.UserRole>().AnyAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == role.Id);
        if (!userRoleExists)
        {
            dbContext.Set<Kpett.ChatApp.Models.UserRole>().Add(new Kpett.ChatApp.Models.UserRole
            {
                UserId = adminUser.Id,
                RoleId = role.Id,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }
    }
}

// Ensure upload directories exist with write permissions for the container user
var webRoot = app.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
foreach (var sub in new[] { "images", "videos", "posts" })
{
    var dir = Path.Combine(webRoot, "uploads", sub);
    if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);
}

app.MapControllers();
app.MapHub<AppHub>("/hubs/app").RequireCors("ClientCors");

app.Run();


