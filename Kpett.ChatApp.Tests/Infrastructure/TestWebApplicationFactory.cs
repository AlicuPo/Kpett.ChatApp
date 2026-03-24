using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kpett.ChatApp.Tests.Infrastructure;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:KpettChatAppDb"] = "Server=(localdb)\\mssqllocaldb;Database=KpettChatAppTests;Trusted_Connection=True;",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["CloudinarySettings:CloudName"] = "test",
                ["CloudinarySettings:ApiKey"] = "test",
                ["CloudinarySettings:ApiSecret"] = "test",
                ["JwtSection:Issuer"] = "test-issuer",
                ["JwtSection:Audience"] = "test-audience",
                ["JwtSection:KeyAccess"] = "12345678901234567890123456789012",
                ["JwtSection:KeyRefres"] = "12345678901234567890123456789012"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(AppDbContext));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll(typeof(IRedisService));
            services.AddSingleton<IRedisService, TestRedisService>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public HttpClient CreateAuthenticatedClient(string userId, string? email = null)
    {
        var client = CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-Email", email ?? $"{userId}@example.com");
        return client;
    }

    public async Task SeedAsync(Action<AppDbContext> seedAction)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seedAction(dbContext);
        await dbContext.SaveChangesAsync();
    }
}
