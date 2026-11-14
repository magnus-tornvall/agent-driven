using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Todo.Api.Tests;

/// Own app instance: this test recreates the database.
public class SchemaRecoveryTests(AppFixture f) : IClassFixture<AppFixture>
{
    [Fact]
    public async Task A_recreated_empty_database_is_brought_back_without_restarting_the_api()
    {
        var ct = TestContext.Current.CancellationToken;
        await Eventually.Status(f.Api, "/health/ready", HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Created, (await f.Api.PostAsJsonAsync("/api/todos", new { title = "before" }, ct)).StatusCode);

        // Under Aspire a stopped and started PostgreSQL container comes back empty (no volume).
        await f.App.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StopCommand, ct);
        await Eventually.Status(f.Api, "/health/ready", HttpStatusCode.ServiceUnavailable);
        await f.App.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StartCommand, ct);

        // While it recovers, readiness must never say 200 while the todo list cannot be served.
        var deadline = DateTime.UtcNow.AddSeconds(90);
        var recovered = false;
        while (DateTime.UtcNow < deadline && !recovered)
        {
            HttpStatusCode ready, todos;
            try
            {
                ready = (await f.Api.GetAsync("/health/ready", ct)).StatusCode;
                todos = (await f.Api.GetAsync("/api/todos", ct)).StatusCode;
            }
            catch (HttpRequestException) { await Task.Delay(500, ct); continue; }

            if (ready == HttpStatusCode.OK)
            {
                // (todos was read after ready: if ready was 200 the list must already work)
                Assert.True(todos == HttpStatusCode.OK, $"/health/ready said 200 but /api/todos returned {(int)todos}");
                recovered = true;
            }
            else await Task.Delay(300, ct);
        }
        Assert.True(recovered, "the API never became ready again after the database was recreated");

        var created = await f.Api.PostAsJsonAsync("/api/todos", new { title = "after" }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
}
