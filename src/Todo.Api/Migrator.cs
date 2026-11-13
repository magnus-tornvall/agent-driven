using System.Reflection;
using Npgsql;

namespace Todo.Api;

/// Applies embedded Migrations/NNN_name.sql files in name order, once each,
/// recorded in schema_migrations. An advisory lock serialises concurrent API instances.
public static class Migrator
{
    private const long LockKey = 7_261_001;

    public static async Task MigrateAsync(NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await Exec(conn, "SELECT pg_advisory_lock(@k)", ct, ("k", LockKey));
        try
        {
            await Exec(conn, "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())", ct);
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order(StringComparer.Ordinal))
            {
                var name = res[(res.IndexOf("Migrations.", StringComparison.Ordinal) + "Migrations.".Length)..];
                await using var check = new NpgsqlCommand("SELECT 1 FROM schema_migrations WHERE name = @n", conn);
                check.Parameters.AddWithValue("n", name);
                if (await check.ExecuteScalarAsync(ct) is not null) continue;

                using var reader = new StreamReader(asm.GetManifestResourceStream(res)!);
                var sql = await reader.ReadToEndAsync(ct);
                await using var tx = await conn.BeginTransactionAsync(ct);
                await using (var run = new NpgsqlCommand(sql, conn, tx)) await run.ExecuteNonQueryAsync(ct);
                await using (var rec = new NpgsqlCommand("INSERT INTO schema_migrations (name) VALUES (@n)", conn, tx))
                {
                    rec.Parameters.AddWithValue("n", name);
                    await rec.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
        }
        finally
        {
            await Exec(conn, "SELECT pg_advisory_unlock(@k)", CancellationToken.None, ("k", LockKey));
        }
    }

    private static async Task Exec(NpgsqlConnection c, string sql, CancellationToken ct, params (string, object)[] ps)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
