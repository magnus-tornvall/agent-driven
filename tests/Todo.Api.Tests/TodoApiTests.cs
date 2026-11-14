using System.Net;
using System.Net.Http.Json;

namespace Todo.Api.Tests;

[Collection("app")]
public class TodoApiTests(AppFixture f)
{
    static CancellationToken Ct => TestContext.Current.CancellationToken;
    record Item(Guid Id, string Title, bool Completed);

    [Fact]
    public async Task Empty_database_is_brought_to_working_state_on_start()
    {
        // Done 7: the fixture started an empty PostgreSQL; the API must have created the schema itself.
        var res = await f.Api.GetAsync("/api/todos", Ct);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(await res.Content.ReadFromJsonAsync<List<Item>>(Ct));
    }

    [Fact]
    public async Task Add_list_complete_delete_roundtrip()
    {
        var created = await f.Api.PostAsJsonAsync("/api/todos", new { title = "  buy milk  " }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<Item>(Ct))!;
        Assert.Equal("buy milk", item.Title);
        Assert.False(item.Completed);

        Assert.Contains((await f.Api.GetFromJsonAsync<List<Item>>("/api/todos", Ct))!, i => i.Id == item.Id);

        var done = await f.Api.PutAsJsonAsync($"/api/todos/{item.Id}", new { completed = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.True((await f.Api.GetFromJsonAsync<List<Item>>("/api/todos", Ct))!.Single(i => i.Id == item.Id).Completed);

        Assert.Equal(HttpStatusCode.NoContent, (await f.Api.DeleteAsync($"/api/todos/{item.Id}", Ct)).StatusCode);
        Assert.DoesNotContain((await f.Api.GetFromJsonAsync<List<Item>>("/api/todos", Ct))!, i => i.Id == item.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Blank_title_is_rejected(string? title)
    {
        var res = await f.Api.PostAsJsonAsync("/api/todos", new { title }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_id_gives_404_on_update_and_delete()
    {
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await f.Api.PutAsJsonAsync($"/api/todos/{id}", new { completed = true }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Api.DeleteAsync($"/api/todos/{id}", Ct)).StatusCode);
    }
}
