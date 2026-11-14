using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Todo.Api.Tests;

/// Starts the real AppHost model (API + PostgreSQL in Docker), without the web app.
/// Each instance is a separate run with its own PostgreSQL: tests that change the database (stop it, recreate
/// it) take their own instance, so no test depends on what ran before it.
/// Ports are not randomised (as in `aspire run`), so two instances only work if the AppHost lets Aspire
/// assign the API port.
public sealed class AppFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    public HttpClient Api { get; private set; } = null!;
    public DistributedApplication App => _app;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Todo_AppHost>(
            ["Todo:WithWeb=false", "DcpPublisher:RandomizePorts=false"]);
        _app = await builder.BuildAsync();
        await _app.StartAsync();
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("api").WaitAsync(TimeSpan.FromSeconds(90));
        Api = _app.CreateHttpClient("api");
    }

    public async ValueTask DisposeAsync()
    {
        Api?.Dispose();
        await _app.DisposeAsync();
    }
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<AppFixture>;
