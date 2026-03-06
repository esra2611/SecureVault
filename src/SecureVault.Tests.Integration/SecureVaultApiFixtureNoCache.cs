using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureVault.Api;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Tests.Integration.TestDoubles;

namespace SecureVault.Tests.Integration;

/// <summary>Fixture with no-op cache to assert DB is source of truth (create/reveal/expiry correct without cache).</summary>
public sealed class SecureVaultApiFixtureNoCache : CustomWebApplicationFactory
{
    public SecureVaultApiFixtureNoCache(TestContainersFixture fixture)
        : base(fixture)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuditPublisher>();
            services.AddSingleton<IAuditPublisher, NoOpAuditPublisher>();
            services.RemoveAll<ISecretCache>();
            services.AddSingleton<ISecretCache, NoOpSecretCache>();
            services.RemoveAll<IRateLimitService>();
            services.AddSingleton<IRateLimitService, AllowAllRateLimitService>();
        });
    }
}
