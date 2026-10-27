using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Orbit.Client.Services;

/// <summary>Flat projection of a droplet for the globe JS module (camelCased on the wire).</summary>
public sealed record GlobeDroplet
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public double Lat { get; init; }
    public double Lng { get; init; }
    public string Status { get; init; } = "";
    public int Health { get; init; }
    public double? Cpu { get; init; }
    public double? Memory { get; init; }
    public double? Disk { get; init; }
    public bool ActionInProgress { get; init; }
    public bool IsOn { get; init; }
    public string RegionSlug { get; init; } = "";
    public string RegionName { get; init; } = "";
    public string SizeSlug { get; init; } = "";
}

/// <summary>Thin wrapper over the <c>orbit-globe.js</c> ES module that renders the globe.gl scene.</summary>
public sealed class GlobeInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    // New token each app start → the browser can't serve a stale cached copy of the JS module.
    private static readonly string CacheBust = Guid.NewGuid().ToString("N")[..8];

    private async Task<IJSObjectReference> ModuleAsync()
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", $"./js/orbit-globe.js?v={CacheBust}");

    public async Task InitAsync<T>(ElementReference container, DotNetObjectReference<T> handler) where T : class
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("init", container, handler);
    }

    public async Task SetDropletsAsync(IReadOnlyList<GlobeDroplet> droplets)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setDroplets", droplets);
    }

    public async Task FocusAsync(long dropletId)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("focus", dropletId);
    }

    public async Task ResetViewAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("resetView");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync("dispose");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // circuit already gone — nothing to clean up
        }
        _module = null;
    }
}
