using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecureVault.Api;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Tests.Integration.TestDoubles;

namespace SecureVault.Tests.Integration;

public sealed class SecureVaultApiFixture : CustomWebApplicationFactory
{
    public SecureVaultApiFixture(TestContainersFixture fixture)
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
            services.RemoveAll<IRateLimitService>();
            services.AddSingleton<IRateLimitService, AllowAllRateLimitService>();
        });
    }
}
