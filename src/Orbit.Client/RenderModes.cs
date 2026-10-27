using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Orbit.Client;

/// <summary>
/// Shared render modes. Orbit's interactive pages depend on WASM-only services (HttpClient,
/// the SignalR client, JS interop for the globe), so they render WebAssembly with prerendering
/// disabled — there's nothing meaningful to render on the server anyway.
/// </summary>
public static class RenderModes
{
    public static readonly IComponentRenderMode Wasm = new InteractiveWebAssemblyRenderMode(prerender: false);
}
