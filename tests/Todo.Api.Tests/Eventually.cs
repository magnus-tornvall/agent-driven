using System.Net;

namespace Todo.Api.Tests;

static class Eventually
{
    public static async Task Status(HttpClient c, string path, HttpStatusCode expected, int seconds = 90)
    {
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        HttpStatusCode last = default;
        while (DateTime.UtcNow < deadline)
        {
            try { last = (await c.GetAsync(path, ct)).StatusCode; if (last == expected) return; } catch (HttpRequestException) { }
            await Task.Delay(500, ct);
        }
        Assert.Fail($"{path} never became {expected}; last {last}");
    }
}
