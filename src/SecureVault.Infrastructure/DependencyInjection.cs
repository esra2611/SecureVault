using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Infrastructure.Caching;
using SecureVault.Infrastructure.Config;
using SecureVault.Infrastructure.Crypto;
using SecureVault.Infrastructure.Jobs;
using SecureVault.Infrastructure.Messaging;
using SecureVault.Infrastructure.Persistence;
using SecureVault.Infrastructure.RateLimiting;
using SecureVault.Infrastructure.Time;
using StackExchange.Redis;

namespace SecureVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.Configure<Pbkdf2Options>(configuration.GetSection(Pbkdf2Options.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<RedisRateLimitOptions>(configuration.GetSection(RedisRateLimitOptions.SectionName));
        services.Configure<SecretExpiryOptions>(configuration.GetSection(SecretExpiryOptions.SectionName));
        services.Configure<RevealSecurityOptions>(configuration.GetSection(RevealSecurityOptions.SectionName));

        services.AddDbContext<SecretVaultDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly("SecureVault.Infrastructure"));
        });
        services.AddScoped<ISecretRepository, SecretRepository>();

        var redisConfig = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options => { options.Configuration = redisConfig; });
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));
        services.AddSingleton<IRateLimitBackend, RedisRateLimitBackend>();
        services.AddSingleton<ISecretCache, SecretCache>();

        services.AddSingleton<IKeyProvider, ConfigKeyProvider>();
        services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
        services.AddSingleton<IPasswordDerivation, Pbkdf2PasswordDerivation>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddSingleton<ITimeProvider, UtcTimeProvider>();

        services.AddSingleton<IAuditPublisher, RabbitMqAuditPublisher>();
        services.AddSingleton<IRateLimitService, RedisRateLimitService>();
        services.AddSingleton<ISecretExpiryConfig, SecretExpiryConfig>();
        services.AddSingleton<IRevealSecurityConfig, RevealSecurityConfig>();
        services.AddHostedService<SecretCleanupHostedService>();

        return services;
    }
}
