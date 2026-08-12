/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Reflection;

namespace HappyGopher.Plugins;

/// <summary>
/// Loads discovered HappyGopher plugins into isolated assembly load contexts.
/// </summary>
public sealed class GopherPluginLoader
{
    public GopherLoadedPlugin Load(GopherPluginDescriptor plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        try
        {
            HappyGopherPluginLoadContext loadContext = new(plugin);
            Assembly entryAssembly = loadContext.LoadFromAssemblyPath(plugin.EntryAssemblyPath);

            return new GopherLoadedPlugin(plugin, entryAssembly);
        }
        catch (Exception ex)
            when (ex is FileNotFoundException
                or FileLoadException
                or BadImageFormatException
                or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to load Gopher plugin '{plugin.Id}' " +
                $"from '{plugin.EntryAssemblyPath}'.",
                ex);
        }
    }
}