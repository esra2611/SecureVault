using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureVault.Api;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Tests.Integration.TestDoubles;

namespace SecureVault.Tests.Integration;

/// <summary>Fixture with audit spy to assert PublishRevealedAsync called on successful reveal.</summary>
public sealed class SecureVaultApiFixtureWithAuditSpy : CustomWebApplicationFactory
{
    private readonly SpyAuditPublisher _auditSpy = new();

    public SecureVaultApiFixtureWithAuditSpy(TestContainersFixture fixture)
        : base(fixture)
    {
    }

    public SpyAuditPublisher AuditSpy => _auditSpy;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuditPublisher>();
            services.AddSingleton<IAuditPublisher>(_auditSpy);
            services.RemoveAll<IRateLimitService>();
            services.AddSingleton<IRateLimitService, AllowAllRateLimitService>();
        });
    }
}
