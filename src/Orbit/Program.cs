using Orbit.Components;
using Orbit.Core;
using Orbit.Core.Services;
using Orbit.Endpoints;
using Orbit.Hubs;
using Orbit.Services;
using Orbit.Shared;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App (WebAssembly interactivity) + live metrics hub.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR();

// Orbit domain: SQLite, encrypted tokens, DigitalOcean provider, services, metrics poller.
builder.Services.AddOrbitCore(builder.Configuration);

// Replace the no-op notifier registered by AddOrbitCore with the real SignalR one.
builder.Services.AddSingleton<IMetricsNotifier, SignalRMetricsNotifier>();

// Live SSH terminal sessions (one shell per SignalR connection).
builder.Services.AddSingleton<SshTerminalSessions>();

var app = builder.Build();

// Create/upgrade the database on startup.
await app.Services.InitializeOrbitDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapOrbitApi();
app.MapHub<MetricsHub>(ApiRoutes.Hubs.Metrics);
app.MapHub<SshTerminalHub>(ApiRoutes.Hubs.Terminal);
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Orbit.Client._Imports).Assembly);

app.Run();
