var builder = DistributedApplication.CreateBuilder(args);

// Each AppHost run gets its own PostgreSQL (Decision 4).
var postgres = builder.AddPostgres("postgres");
var db = postgres.AddDatabase("tododb");

var api = builder.AddProject<Projects.Todo_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health/ready");

// Tests start the model without the web app (Todo:WithWeb=false) so they need no npm.
if (builder.Configuration["Todo:WithWeb"] != "false")
{
    // The web app takes the API's address from its environment and forwards /api to it.
    builder.AddViteApp("web", "../../web")
        .WithEnvironment("API_URL", api.GetEndpoint("http"))
        .WaitFor(api)
        .WithExternalHttpEndpoints();
}

builder.Build().Run();
