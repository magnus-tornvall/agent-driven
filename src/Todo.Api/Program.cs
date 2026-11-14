using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Todo.Api;

var builder = WebApplication.CreateBuilder(args);

// The only thing the API knows about the database: the connection string named "tododb"
// (ConnectionStrings__tododb in the environment).
builder.AddNpgsqlDataSource("tododb", configureSettings: s => s.DisableHealthChecks = true);
builder.Services.AddHostedService<MigrationService>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// Liveness: the process is up. No dependencies.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness: includes the database.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

var api = app.MapGroup("/api/todos");

api.MapGet("/", async (NpgsqlDataSource db, CancellationToken ct) =>
{
    await using var cmd = db.CreateCommand("SELECT id, title, completed FROM todos ORDER BY created_at, id");
    await using var r = await cmd.ExecuteReaderAsync(ct);
    var list = new List<TodoItem>();
    while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetString(1), r.GetBoolean(2)));
    return list;
});

api.MapPost("/", async (NewTodo body, NpgsqlDataSource db, CancellationToken ct) =>
{
    var title = body.Title?.Trim();
    if (string.IsNullOrEmpty(title)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Title is required."] });
    var id = Guid.CreateVersion7();
    await using var cmd = db.CreateCommand("INSERT INTO todos (id, title) VALUES ($1, $2)");
    cmd.Parameters.AddWithValue(id);
    cmd.Parameters.AddWithValue(title);
    await cmd.ExecuteNonQueryAsync(ct);
    return Results.Created($"/api/todos/{id}", new TodoItem(id, title, false));
});

api.MapPut("/{id:guid}", async (Guid id, UpdateTodo body, NpgsqlDataSource db, CancellationToken ct) =>
{
    await using var cmd = db.CreateCommand("UPDATE todos SET completed = $2 WHERE id = $1 RETURNING title");
    cmd.Parameters.AddWithValue(id);
    cmd.Parameters.AddWithValue(body.Completed);
    return await cmd.ExecuteScalarAsync(ct) is string title
        ? Results.Ok(new TodoItem(id, title, body.Completed))
        : Results.NotFound();
});

api.MapDelete("/{id:guid}", async (Guid id, NpgsqlDataSource db, CancellationToken ct) =>
{
    await using var cmd = db.CreateCommand("DELETE FROM todos WHERE id = $1");
    cmd.Parameters.AddWithValue(id);
    return await cmd.ExecuteNonQueryAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
});

app.Run();

public record TodoItem(Guid Id, string Title, bool Completed);
public record NewTodo(string? Title);
public record UpdateTodo(bool Completed);
public partial class Program;
