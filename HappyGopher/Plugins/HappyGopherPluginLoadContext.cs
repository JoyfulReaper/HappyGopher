/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using System.Reflection;
using System.Runtime.Loader;

namespace HappyGopher.Plugins;

/// <summary>
/// Provides an isolated assembly load context for a HappyGopher plugin.
/// </summary>
internal sealed class HappyGopherPluginLoadContext : AssemblyLoadContext
{
    private static readonly Assembly ExtensibilityAssembly = typeof(IGopherPage).Assembly;
    private static readonly string ExtensibilityAssemblyName = ExtensibilityAssembly.GetName().Name!;
    private readonly AssemblyDependencyResolver _resolver;

    public HappyGopherPluginLoadContext(GopherPluginDescriptor plugin)
        : base(name: $"HappyGopher.Plugin:{plugin.Id}", isCollectible: false)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        _resolver = new AssemblyDependencyResolver(plugin.EntryAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // All plugins must use the host's copy of the
        // HappyGopher extensibility contract.
        if (string.Equals(
                assemblyName.Name,
                ExtensibilityAssemblyName,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExtensibilityAssembly;
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (libraryPath is null)
        {
            return nint.Zero;
        }

        return LoadUnmanagedDllFromPath(libraryPath);
    }
}