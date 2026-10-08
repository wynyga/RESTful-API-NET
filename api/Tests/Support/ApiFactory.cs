using System.Net.Http.Headers;
using System.Net.Http.Json;
using Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Tests.Support
{
    /// <summary>
    /// Hosts the real application against an in-memory SQLite database. Each factory
    /// (one per test class) gets its own database, so classes never see each other's data.
    /// </summary>
    public class ApiFactory : WebApplicationFactory<Program>
    {
        public const string AdminEmail = "admin@test.local";
        public const string Password = "Passw0rd!";

        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        public ApiFactory()
        {
            _connection.Open();
        }

        /// <summary>Override or add configuration values for a particular test class.</summary>
        protected virtual void Customize(IDictionary<string, string?> settings) { }

        /// <summary>Replace or add services (for example a fake HTTP handler) for a particular test class.</summary>
        protected virtual void ConfigureTestServices(IServiceCollection services) { }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            var settings = new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "test-secret-that-is-comfortably-longer-than-32-chars",
                ["JwtSettings:Issuer"] = "ProjectTracker.Tests",
                ["JwtSettings:Audience"] = "ProjectTracker.Tests.Users",
                ["JwtSettings:ExpirationInMinutes"] = "30",
                ["HASHIDS_SALT"] = "test-hashids-salt",
                ["ConnectionStrings:DefaultConnection"] = "unused-in-tests",
                ["Database:AutoMigrate"] = "false",
                ["RateLimiting:AuthPerMinute"] = "1000",
                ["RateLimiting:WeatherPerMinute"] = "1000",
                ["Weather:BaseUrl"] = "http://weather.test/",
                ["Cors:Origins"] = "",
            };
            Customize(settings);

            // Added last, so these win over any .env file the developer has on this machine.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));

            builder.ConfigureServices(services =>
            {
                // EF Core 9 accumulates AddDbContext configuration, so the MySQL setup from Program.cs
                // must be removed too, or it would still apply on top of SQLite.
                // (matched by name because EF keeps that interface out of its public surface).
                foreach (var descriptor in services.Where(d =>
                    d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")
                    && d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)).ToList())
                {
                    services.Remove(descriptor);
                }
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                ConfigureTestServices(services);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }

        // ---- helpers -------------------------------------------------------

        public static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

        /// <summary>Registers a new regular user and returns a client already signed in as them.</summary>
        public async Task<HttpClient> CreateUserClientAsync(string? email = null)
        {
            email ??= NewEmail();
            var client = CreateClient();
            var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
            register.EnsureSuccessStatusCode();
            return await SignInAsync(client, email);
        }

        /// <summary>Creates an Admin directly in the database (there is no public way to) and signs in as them.</summary>
        public async Task<HttpClient> CreateAdminClientAsync()
        {
            var email = NewEmail();
            var client = CreateClient();
            (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users.SingleAsync(u => u.Email == email);
                user.Role = "Admin";
                await db.SaveChangesAsync();
            }
            return await SignInAsync(client, email);
        }

        /// <summary>Logs the (already registered) user in and sets the bearer token on the client.</summary>
        public static async Task<HttpClient> SignInAsync(HttpClient client, string email)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
            login.EnsureSuccessStatusCode();
            var body = await login.Content.ReadFromJsonAsync<LoginBody>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
            return client;
        }

        private sealed record LoginBody(string Token, int ExpiresIn);
    }
}
