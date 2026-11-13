using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Todo.Api.Tests;

/// Own app instance: this test stops the database.
public class HealthTests(AppFixture f) : IClassFixture<AppFixture>
{
    [Fact]
    public async Task Readiness_follows_the_database_and_liveness_does_not()
    {
        await Eventually.Status(f.Api, "/health/ready", HttpStatusCode.OK);

        await f.App.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StopCommand, TestContext.Current.CancellationToken);
        await Eventually.Status(f.Api, "/health/ready", HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.OK, (await f.Api.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode); // a down database must not fail liveness

        await f.App.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StartCommand, TestContext.Current.CancellationToken);
        await Eventually.Status(f.Api, "/health/ready", HttpStatusCode.OK);
    }
}
