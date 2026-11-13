using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Todo.Api.Tests;

[Collection("app")]
public class HealthTests(AppFixture f)
{
    private async Task<HttpStatusCode> Status(string path) => (await f.Api.GetAsync(path)).StatusCode;

    private async Task Eventually(string path, HttpStatusCode expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        HttpStatusCode last = default;
        while (DateTime.UtcNow < deadline)
        {
            try { last = await Status(path); if (last == expected) return; } catch (HttpRequestException) { }
            await Task.Delay(500);
        }
        Assert.Fail($"{path} never became {expected}; last {last}");
    }

    [Fact]
    public async Task Readiness_follows_the_database_and_liveness_does_not()
    {
        await Eventually("/health/ready", HttpStatusCode.OK);

        await f.App.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StopCommand);
        await Eventually("/health/ready", HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.OK, await Status("/health/live")); // a down database must not fail liveness

        await f.App.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StartCommand);
        await Eventually("/health/ready", HttpStatusCode.OK);
    }
}
