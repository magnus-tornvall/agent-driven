using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Todo.Api;

/// Tracks whether the schema is in place; readiness needs it.
public sealed class SchemaState { public volatile bool Ready; }

/// Retries until the database is reachable, then migrates. The API process stays up meanwhile;
/// readiness reports not-ready until this finishes.
public sealed class MigrationService(NpgsqlDataSource db, SchemaState state, ILogger<MigrationService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        for (var delay = 1000; !ct.IsCancellationRequested; delay = Math.Min(delay * 2, 10_000))
        {
            try
            {
                await Migrator.MigrateAsync(db, ct);
                state.Ready = true;
                log.LogInformation("Database schema is up to date");
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Database not ready ({Message}); retrying in {Delay} ms", e.Message, delay);
                try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }
}

/// Readiness only (not liveness): a down database must not make an orchestrator restart the API.
public sealed class DatabaseHealthCheck(NpgsqlDataSource db, SchemaState state) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!state.Ready) return HealthCheckResult.Unhealthy("Schema not yet applied");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await using var conn = await db.OpenConnectionAsync(timeout.Token);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", e);
        }
    }
}
