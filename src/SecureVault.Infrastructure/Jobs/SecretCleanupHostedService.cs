using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Jobs;

/// <summary>
/// Periodically deletes terminal secret rows (expired or already revealed). Safe and idempotent.
/// Does not affect correctness: reveal path never returns data for such rows.
/// Ref: OWASP ASVS data retention; RabbitMQ at-least-once semantics if cleanup were event-driven.
/// </summary>
public sealed class SecretCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITimeProvider _time;
    private readonly ILogger<SecretCleanupHostedService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public SecretCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ITimeProvider time,
        ILogger<SecretCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Secret cleanup job failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task DoCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISecretRepository>();
            var utcNow = _time.UtcNow;
            var deleted = await repository.DeleteTerminalRowsAsync(utcNow, cancellationToken);
            if (deleted > 0)
                _logger.LogInformation("Secret cleanup deleted {Count} terminal rows.", deleted);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Relation "Secrets" does not exist — table not created yet or wrong schema; skip this run.
            _logger.LogDebug("Secret cleanup skipped: Secrets table not found (42P01).");
        }
    }
}
