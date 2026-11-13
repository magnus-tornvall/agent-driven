using System.Net.Http.Json;
using Aspire.Hosting.Testing;

namespace Todo.Api.Tests;

/// Two runs of the AppHost at once (two worktrees) must not share an API or a database.
public class TwoRunsTests(AppFixture first) : IClassFixture<AppFixture>
{
    record Item(Guid Id, string Title, bool Completed);

    [Fact]
    public async Task Two_runs_get_different_api_addresses_and_separate_data()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var second = new AppFixture();
        await second.InitializeAsync();

        Assert.NotEqual(first.Api.BaseAddress, second.Api.BaseAddress);

        var title = $"only in the first run {Guid.NewGuid()}";
        (await first.Api.PostAsJsonAsync("/api/todos", new { title }, ct)).EnsureSuccessStatusCode();
        var inSecond = await second.Api.GetFromJsonAsync<List<Item>>("/api/todos", ct);
        Assert.DoesNotContain(inSecond!, i => i.Title == title);
    }
}
