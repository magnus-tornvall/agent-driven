using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Todo.Api.Tests;

/// Starts the real AppHost model (API + PostgreSQL in Docker), without the web app.
public sealed class AppFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    public HttpClient Api { get; private set; } = null!;
    public DistributedApplication App => _app;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Todo_AppHost>(["Todo:WithWeb=false"]);
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
