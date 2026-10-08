using System.Net;
using System.Net.Http.Json;
using API;
using Data;
using Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models.Options;
using Tests.Support;

namespace Tests
{
    public class HealthTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public HealthTests(ApiFactory factory) => _factory = factory;

        [Theory]
        [InlineData("/healthz")]
        [InlineData("/readyz")]
        public async Task HealthEndpointsAreOpenAndHealthy(string path)
        {
            var response = await _factory.CreateClient().GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ErrorsComeBackAsProblemDetails()
        {
            var response = await _factory.CreateClient().GetAsync("/api/does-not-exist");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    public class CorsTests : IClassFixture<CorsTests.CorsFactory>
    {
        public sealed class CorsFactory : ApiFactory
        {
            protected override void Customize(IDictionary<string, string?> settings) =>
                settings["Cors:Origins"] = "http://localhost:3002, https://app.example.test";
        }

        private readonly CorsFactory _factory;

        public CorsTests(CorsFactory factory) => _factory = factory;

        private async Task<HttpResponseMessage> PreflightAsync(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/projects");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization");
            return await _factory.CreateClient().SendAsync(request);
        }

        [Theory]
        [InlineData("http://localhost:3002")]
        [InlineData("https://app.example.test")]
        public async Task ListedOriginsAreAllowed(string origin)
        {
            var response = await PreflightAsync(origin);

            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed));
            Assert.Equal(origin, allowed!.Single());
        }

        [Fact]
        public async Task OtherOriginsAreNotAllowed()
        {
            var response = await PreflightAsync("https://evil.example.test");

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    public class NoCorsConfiguredTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public NoCorsConfiguredTests(ApiFactory factory) => _factory = factory;

        [Fact]
        public async Task WithoutConfiguredOriginsNoCrossOriginAccessIsGranted()
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/projects");
            request.Headers.Add("Origin", "http://localhost:3002");
            request.Headers.Add("Access-Control-Request-Method", "GET");

            var response = await _factory.CreateClient().SendAsync(request);

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    public class RateLimitTests : IClassFixture<RateLimitTests.LimitedFactory>
    {
        public sealed class LimitedFactory : ApiFactory
        {
            protected override void Customize(IDictionary<string, string?> settings) =>
                settings["RateLimiting:AuthPerMinute"] = "3";
        }

        private readonly LimitedFactory _factory;

        public RateLimitTests(LimitedFactory factory) => _factory = factory;

        [Fact]
        public async Task LoginAttemptsBeyondTheLimitAreRefused()
        {
            var client = _factory.CreateClient();
            var statuses = new List<HttpStatusCode>();

            for (var i = 0; i < 5; i++)
            {
                var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.local", password = "wrong-password" });
                statuses.Add(response.StatusCode);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                }
            }

            Assert.Equal(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests }, statuses);
        }
    }

    public class StartupValidationTests
    {
        private sealed class WeakSecretFactory : ApiFactory
        {
            protected override void Customize(IDictionary<string, string?> settings) =>
                settings["JwtSettings:Secret"] = "too-short";
        }

        private sealed class NoSaltFactory : ApiFactory
        {
            protected override void Customize(IDictionary<string, string?> settings) =>
                settings["HASHIDS_SALT"] = "";
        }

        [Fact]
        public void AWeakJwtSecretStopsTheAppAtStartup()
        {
            using var factory = new WeakSecretFactory();

            var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            Assert.Contains("JwtSettings:Secret", error.ToString());
        }

        [Fact]
        public void AMissingHashidsSaltStopsTheAppAtStartup()
        {
            using var factory = new NoSaltFactory();

            var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            Assert.Contains("HASHIDS_SALT", error.ToString());
        }

        [Fact]
        public void JwtSettingsRequireALongSecret()
        {
            var settings = new JwtSettings { Secret = "short", Issuer = "i", Audience = "a" };
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                settings, new System.ComponentModel.DataAnnotations.ValidationContext(settings), results, validateAllProperties: true);

            Assert.False(valid);
        }
    }

    public class DataSeederTests
    {
        private static async Task<ServiceProvider> BuildAsync(Dictionary<string, string?> config)
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();

            var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            return provider;
        }

        [Fact]
        public async Task NothingIsCreatedUnlessConfigured()
        {
            await using var provider = await BuildAsync(new());

            await DataSeeder.SeedAsync(provider);

            var db = provider.GetRequiredService<AppDbContext>();
            Assert.Empty(db.Users);
            Assert.Empty(db.Projects);
        }

        [Fact]
        public async Task CreatesTheFirstAdminOnceAndHashesThePassword()
        {
            await using var provider = await BuildAsync(new() { ["SEED_ADMIN_EMAIL"] = " Boss@Test.Local ", ["SEED_ADMIN_PASSWORD"] = "S3cret-pass" });

            await DataSeeder.SeedAsync(provider);
            await DataSeeder.SeedAsync(provider); // running twice must not duplicate or reset

            var admin = Assert.Single(provider.GetRequiredService<AppDbContext>().Users);
            Assert.Equal("boss@test.local", admin.Email);
            Assert.Equal("Admin", admin.Role);
            Assert.NotEqual("S3cret-pass", admin.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("S3cret-pass", admin.PasswordHash));
        }

        [Fact]
        public async Task SampleDataCoversEveryStatusAndIsNotAddedTwice()
        {
            await using var provider = await BuildAsync(new() { ["SEED_SAMPLE_DATA"] = "true" });

            await DataSeeder.SeedAsync(provider);
            await DataSeeder.SeedAsync(provider);

            var projects = provider.GetRequiredService<IProjectRepository>();
            var summary = await projects.GetSummaryAsync(DateTime.UtcNow.Date);
            Assert.Equal(12, summary.Total);
            Assert.True(summary.Completed > 0);
            Assert.True(summary.Overdue > 0);
            Assert.True(summary.OnProgress > 0);
        }
    }
}
