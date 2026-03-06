using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SecureVault.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SecretVaultDbContext>
{
    public SecretVaultDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=securevault;Username=securevault;Password=securevault";
        var options = new DbContextOptionsBuilder<SecretVaultDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new SecretVaultDbContext(options);
    }
}
