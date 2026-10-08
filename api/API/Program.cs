using System.Text;
using System.Threading.RateLimiting;
using API;
using API.Options;
using Business.Services;
using Data;
using Data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Models.Options;

// Load .env variables by traversing up directories
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

// Settings are validated at startup: a missing or weak secret stops the app right away.
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration(JwtSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitSettings>().BindConfiguration(RateLimitSettings.SectionName);

// Database. Read lazily, so tests and tools can swap the configuration or the provider.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
    // An explicit server version means the app can start before the database is reachable.
    var version = Version.Parse(config["Database:MySqlVersion"] ?? "8.0.36");
    options.UseMySql(connectionString, new MySqlServerVersion(version));
});

// Dependency Injection
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// External weather API
builder.Services.AddHttpClient<IWeatherService, WeatherService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["Weather:BaseUrl"] ?? "https://wttr.in/");
    client.Timeout = TimeSpan.FromSeconds(5); // strict timeout so a slow provider cannot hold requests open
});

// JWT authentication. Validation parameters come from the validated settings above.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwt) =>
    {
        var settings = jwt.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// CORS: only the origins listed in Cors:Origins (comma separated) may call the API from a browser.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, config) =>
{
    var origins = (config["Cors:Origins"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    options.AddPolicy("web", policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

// Rate limiting per client address
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Terlalu banyak permintaan. Coba lagi sebentar lagi.",
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: ct);
    };

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => Window(Limits(context).AuthPerMinute)));
    options.AddPolicy("weather", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => Window(Limits(context).WeatherPerMinute)));

    static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    static RateLimitSettings Limits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
    static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ProjectTracker API", Version = "v1" });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Fail fast: resolving this now surfaces a missing HASHIDS_SALT at startup, not on the first request.
app.Services.GetRequiredService<IEncryptionService>();

await using (var scope = app.Services.CreateAsyncScope())
{
    if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Behind a TLS-terminating proxy or inside a container there is only plain HTTP; set Https:Redirect=false there.
if (app.Configuration.GetValue("Https:Redirect", true))
{
    app.UseHttpsRedirection();
}
app.UseCors("web");
app.UseRateLimiter();

// Use Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// /healthz: the process is up. /readyz: the process can reach its database.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz");

app.Run();

// Lets the integration tests host the app with WebApplicationFactory<Program>.
public partial class Program { }
