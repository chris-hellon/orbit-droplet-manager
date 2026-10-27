using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Orbit.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// All data comes from the Orbit host's minimal API, same origin as the app.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<OrbitApiClient>();
builder.Services.AddScoped<MetricsHubClient>();
builder.Services.AddScoped<GlobeInterop>();

await builder.Build().RunAsync();
