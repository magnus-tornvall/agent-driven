// ContainerTargetPlatform / WithContainerBuildOptions are marked evaluation-only in Aspire 13.4. They are the
// only way to build preview images for this machine's architecture (the default is linux/amd64), so the
// diagnostic is acknowledged for this file, and no other API is affected.
#pragma warning disable ASPIREPIPELINES003
using Aspire.Hosting.Publishing;
using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

// `aspire deploy` turns this model into a Docker Compose file and runs it: that is the per-branch preview.
// Preview:Name (env Preview__Name) names this preview's Compose project, images and port, so previews of
// different branches can run side by side. See scripts/preview.
var previewName = builder.Configuration["Preview:Name"] ?? "preview";
var previewPort = builder.Configuration["Preview:WebPort"];
builder.AddDockerComposeEnvironment(previewName).WithDashboard(false);

// Preview images are built for the machine they run on (the default is linux/amd64).
var platform = RuntimeInformation.OSArchitecture == Architecture.Arm64
    ? ContainerTargetPlatform.LinuxArm64
    : ContainerTargetPlatform.LinuxAmd64;

// Each AppHost run gets its own PostgreSQL (Decision 4).
var postgres = builder.AddPostgres("postgres");
var db = postgres.AddDatabase("tododb");

var api = builder.AddProject<Projects.Todo_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health/ready")
    .WithContainerBuildOptions(o => o.TargetPlatform = platform);

// Tests start the model without the web app (Todo:WithWeb=false) so they need no npm.
if (builder.Configuration["Todo:WithWeb"] != "false")
{
    // The web app takes the API's address from its environment and forwards /api to it.
    builder.AddViteApp("web", "../../web")
        .WithEnvironment("API_URL", api.GetEndpoint("http"))
        .WaitFor(api)
        .WithExternalHttpEndpoints()
        // In a preview the web container is built from web/Dockerfile (nginx: static files, /api forwarded to API_URL).
        .PublishAsDockerFile()
        .PublishAsDockerComposeService((_, service) =>
        {
            // Publish on loopback only; the port is chosen per branch (nothing in the VM listens on 0.0.0.0).
            service.Ports.Clear();
            service.Ports.Add($"127.0.0.1:{previewPort ?? ""}:8000");
        })
        .WithContainerBuildOptions(o => o.TargetPlatform = platform);
}

builder.Build().Run();
