// ContainerTargetPlatform / WithContainerBuildOptions are marked evaluation-only in Aspire 13.6. They are the
// only way to build preview images for this machine's architecture (the default is linux/amd64), so the
// diagnostic is acknowledged for this file, and no other API is affected.
#pragma warning disable ASPIREPIPELINES003
using Aspire.Hosting.Publishing;
using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

// The per-branch preview is this model published as a Docker Compose file (scripts/preview runs
// `aspire do prepare-preview`, then plain `docker compose -p todo-<branch>`). Preview:WebPort
// (env Preview__WebPort) is the host port the preview's web container is published on.
var previewPort = builder.Configuration["Preview:WebPort"];
builder.AddDockerComposeEnvironment("preview").WithDashboard(false);

// Preview images are built for the machine they run on (the default is linux/amd64).
var platform = RuntimeInformation.OSArchitecture == Architecture.Arm64
    ? ContainerTargetPlatform.LinuxArm64
    : ContainerTargetPlatform.LinuxAmd64;

// Each AppHost run gets its own PostgreSQL (Decision 4).
var postgres = builder.AddPostgres("postgres");
// Aspire creates the database itself only while running; a published Compose file would start Postgres
// without it. The postgres image creates POSTGRES_DB on first start.
if (builder.ExecutionContext.IsPublishMode) postgres.WithEnvironment("POSTGRES_DB", "tododb");
var db = postgres.AddDatabase("tododb");

var api = builder.AddProject<Projects.Todo_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    // Aspire assigns the HTTP port, so two runs never collide.
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health/ready")
    .WithContainerBuildOptions(o => o.TargetPlatform = platform);

// Tests start the model without the web app (Todo:WithWeb=false) so they need no npm.
if (builder.Configuration["Todo:WithWeb"] != "false")
{
    // The web app takes the API's address from its environment and forwards /api to it.
    builder.AddViteApp("web", "../../web")
        .WithEnvironment("API_URL", api.GetEndpoint("http"))
        .WaitFor(api)
        // "External" only tells Aspire/Compose the endpoint is for outside callers. Nothing binds beyond
        // loopback: Vite uses its default host, and the preview port is mapped to 127.0.0.1 above.
        .WithExternalHttpEndpoints()
        // In a preview the web container is built from web/Dockerfile (nginx: static files, /api forwarded to API_URL).
        .PublishAsDockerFile()
        .PublishAsDockerComposeService((_, service) =>
        {
            // Published on loopback only (127.0.0.1); the host port is chosen per branch.
            service.Ports.Clear();
            service.Ports.Add($"127.0.0.1:{previewPort ?? ""}:8000");
        })
        .WithContainerBuildOptions(o => o.TargetPlatform = platform);
}

builder.Build().Run();
