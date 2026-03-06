using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureVault.Api;
using SecureVault.Api.Endpoints;
using SecureVault.Api.Middleware;
using SecureVault.Application;
using SecureVault.Application.Secrets.CreateSecret;
using SecureVault.Infrastructure;
using SecureVault.Infrastructure.Persistence;
using Serilog;
using HealthCheckOptions = Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions;

var builder = WebApplication.CreateBuilder(args);

// Structured logging: Serilog (no request/response body; redact sensitive data)
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SecureVault.Api")
    .WriteTo.Console());

// Request body size limit (e.g. 64 KB for create secret payload)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var pgConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddHealthChecks()
    .AddNpgSql(pgConnection!, name: "postgres", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
    .AddRedis(redisConnection, name: "redis", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

var baseUrl = builder.Configuration["SecureVault:BaseUrl"] ?? "http://localhost:3000";
builder.Services.AddSingleton<ICreateSecretLinkBuilder>(_ => new LinkBuilder(baseUrl));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SecureVault API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:Origins"]?.Split(',') ?? ["http://localhost:3000"])
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

ValidateEncryptionConfig(builder.Configuration);

var app = builder.Build();

MapIntegrationTestDiagnostics(app);
await ValidateCriticalDependenciesAsync(app);
ConfigureHttpPipeline(app);
MapAppEndpoints(app);
await RunMigrationsIfEnabledAsync(app, builder.Configuration);

app.Logger.LogInformation("API reachable at http://localhost:8080 (host) or http://api:8080 (Docker network). Health: http://localhost:8080/health");

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

static void ValidateEncryptionConfig(IConfiguration config)
{
    var encryptionKey = config["Encryption:MasterKeyBase64"];
    var hasKeysSection = config.GetSection("Encryption:Keys").GetChildren().Any();
    if (string.IsNullOrWhiteSpace(encryptionKey) && !hasKeysSection)
    {
        throw new InvalidOperationException(
            "Encryption:MasterKeyBase64 or Encryption:Keys is required. Set MasterKeyBase64 in appsettings or environment (e.g. Encryption__MasterKeyBase64), or use Encryption:Keys:1, Keys:2 for rotation. " +
            "Generate a 32-byte key: [Convert]::ToBase64String((1..32|%{[byte](Get-Random -Max 256)}))");
    }
    if (hasKeysSection)
        return;
    try
    {
        var keyBytes = Convert.FromBase64String(encryptionKey!);
        if (keyBytes.Length != 32)
            throw new InvalidOperationException("Encryption:MasterKeyBase64 must be a base64-encoded 32-byte (256-bit) value.");
    }
    catch (FormatException)
    {
        throw new InvalidOperationException("Encryption:MasterKeyBase64 must be valid base64.");
    }
}

static void MapIntegrationTestDiagnostics(WebApplication app)
{
    if (!app.Environment.IsEnvironment("IntegrationTests"))
        return;
    var effectiveRedis = app.Configuration.GetConnectionString("Redis") ?? "(null)";
    app.Logger.LogInformation("IntegrationTests: Effective Redis config = {Redis}", effectiveRedis);
    app.MapGet("/test-config/redis", (IConfiguration config) =>
        Results.Ok(new { redis = config.GetConnectionString("Redis") ?? "(null)" }))
        .ExcludeFromDescription();
}

static async Task ValidateCriticalDependenciesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var pgConn = app.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(pgConn))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
    try
    {
        await using var db = scope.ServiceProvider.GetRequiredService<SecretVaultDbContext>();
        if (await db.Database.CanConnectAsync())
            logger.LogInformation("Startup: Postgres connection OK.");
        else
            throw new InvalidOperationException("Postgres connection failed.");
    }
    catch (Exception ex)
    {
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Postgres is required at startup.", ex);
        logger.LogWarning(ex, "Startup: Postgres check failed (Development).");
    }
    try
    {
        var redis = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        await redis.SetAsync("_startup", System.Text.Encoding.UTF8.GetBytes("1"), new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1) });
        logger.LogInformation("Startup: Redis connection OK.");
    }
    catch (Exception ex)
    {
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Redis is required at startup.", ex);
        logger.LogWarning(ex, "Startup: Redis check failed (Development).");
    }
}

static void ConfigureHttpPipeline(WebApplication app)
{
    app.UseCors();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseSerilogRequestLogging(options => options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode}"); // No body, no sensitive data
    app.UseMiddleware<RateLimitMiddleware>();
    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}

static void MapAppEndpoints(WebApplication app)
{
    app.MapSecretEndpoints();
    app.MapShareableLinkEndpoint();
    app.MapHealthChecks("/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    }).ExcludeFromDescription();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).ExcludeFromDescription();
}

static async Task RunMigrationsIfEnabledAsync(WebApplication app, IConfiguration config)
{
    var runMigrations = app.Environment.IsDevelopment() || config.GetValue<bool>("RunMigrations");
    if (!runMigrations)
        return;
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SecretVaultDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        var pendingList = pending.ToList();
        if (pendingList.Count > 0)
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pendingList.Count, string.Join(", ", pendingList));
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");
    }
    catch (Exception ex)
    {
        if (app.Environment.IsDevelopment())
        {
            logger.LogWarning(ex, "MigrateAsync failed; attempting EnsureCreated in Development.");
            var created = await db.Database.EnsureCreatedAsync();
            logger.LogInformation("EnsureCreated returned {Created}. Schema should exist.", created);
        }
        else
            throw;
    }

    // Ensure Secrets and AuditLogs exist (idempotent). Handles: Docker/Production where migration history may exist but table was never created, or Development with empty DB.
    await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""Secrets"" (
    ""Id"" uuid NOT NULL,
    ""TokenHashBase64"" character varying(64) NOT NULL,
    ""ExpiryType"" integer NOT NULL,
    ""UtcCreatedAt"" timestamp with time zone NOT NULL,
    ""UtcExpiresAt"" timestamp with time zone NOT NULL,
    ""UtcRevealedAt"" timestamp with time zone NULL,
    ""Ciphertext"" bytea NULL,
    ""Nonce"" bytea NULL,
    ""SaltForPassword"" bytea NULL,
    ""KeyVersion"" integer NOT NULL DEFAULT 1,
    ""IsPasswordProtected"" boolean NOT NULL DEFAULT false,
    ""PasswordHashBase64"" character varying(256) NULL,
    CONSTRAINT ""PK_Secrets"" PRIMARY KEY (""Id"")
);
");
    await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Secrets_TokenHashBase64"" ON ""Secrets"" (""TokenHashBase64"");
");
    await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND LOWER(table_name) = 'secrets' AND LOWER(column_name) = 'ispasswordprotected') THEN
        ALTER TABLE ""Secrets"" ADD COLUMN ""IsPasswordProtected"" boolean NOT NULL DEFAULT false;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND LOWER(table_name) = 'secrets' AND LOWER(column_name) = 'passwordhashbase64') THEN
        ALTER TABLE ""Secrets"" ADD COLUMN ""PasswordHashBase64"" character varying(256) NULL;
    END IF;
END $$;
");
    await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""AuditLogs"" (
    ""Id"" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""MessageId"" character varying(64) NULL,
    ""EventType"" character varying(64) NOT NULL,
    ""PayloadJson"" text NOT NULL,
    ""OccurredAtUtc"" timestamp with time zone NOT NULL,
    ""CreatedAtUtc"" timestamp with time zone NOT NULL
);
");
    await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AuditLogs_MessageId"" ON ""AuditLogs"" (""MessageId"") WHERE ""MessageId"" IS NOT NULL;
");
    logger.LogInformation("Secrets and AuditLogs tables ensured.");
}

public partial class Program { }
