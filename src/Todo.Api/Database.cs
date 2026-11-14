using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Todo.Api;

/// Keeps the schema in place for as long as the API runs: applies migrations when the database is reachable
/// but not current (first start, or a database that was recreated empty), and retries while it is not
/// reachable. The API process stays up meanwhile; readiness says what is true (see DatabaseHealthCheck).
public sealed class MigrationService(NpgsqlDataSource db, ILogger<MigrationService> log) : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var failures = 0;
        while (!ct.IsCancellationRequested)
        {
            var delay = Poll;
            try
            {
                if (!await Migrator.IsCurrentAsync(db, ct))
                {
                    log.LogInformation("Database schema is missing or behind; applying migrations");
                    await Migrator.MigrateAsync(db, ct);
                    log.LogInformation("Database schema is up to date");
                }
                failures = 0;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failures++;
                delay = TimeSpan.FromMilliseconds(Math.Min(1000 * failures, 10_000));
                log.LogWarning("Database not ready ({Message}); retrying in {Delay} ms", e.Message, delay.TotalMilliseconds);
            }
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { return; }
        }
    }
}

/// Readiness only (not liveness): a down database must not make an orchestrator restart the API.
/// Asks the database each time whether the schema is there; it keeps no memory of earlier answers.
public sealed class DatabaseHealthCheck(NpgsqlDataSource db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            return await Migrator.IsCurrentAsync(db, timeout.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Schema missing or behind");
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", e);
        }
    }
}
