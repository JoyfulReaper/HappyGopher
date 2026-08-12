/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Text.Json;

namespace HappyGopher.Plugins;

/// <summary>
/// Discovers HappyGopher plugins from a plugin directory.
/// </summary>
public sealed class GopherPluginDiscovery
{
    private const string ManifestFileName = "happygopher.plugin.json";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    /// <summary>
    /// Discovers valid plugins from the immediate child directories of
    /// <paramref name="pluginDirectory"/>.
    /// </summary>
    public IReadOnlyList<GopherPluginDescriptor> Discover(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        string pluginRoot = ResolvePluginRoot(pluginDirectory);
        if (!Directory.Exists(pluginRoot))
        {
            return [];
        }

        List<GopherPluginDescriptor> plugins = [];
        foreach (string directory in Directory.EnumerateDirectories(pluginRoot))
        {
            string manifestPath = Path.Combine(directory, ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            GopherPluginManifest manifest = ReadManifest(manifestPath);
            ValidateManifest(manifest, manifestPath);

            string pluginPath = Path.GetFullPath(directory);
            string entryAssemblyPath = Path.GetFullPath(
                manifest.EntryAssembly,
                pluginPath);

            EnsurePathIsInsidePlugin(
                pluginPath,
                entryAssemblyPath,
                manifest.Id);

            if (!File.Exists(entryAssemblyPath))
            {
                throw new FileNotFoundException(
                    $"Entry assembly for Gopher plugin '{manifest.Id}' " +
                    $"was not found: '{entryAssemblyPath}'.",
                    entryAssemblyPath);
            }

            plugins.Add(new GopherPluginDescriptor(
                manifest.Id,
                pluginPath,
                entryAssemblyPath));
        }

        EnsureUniquePluginIds(plugins);

        return plugins
            .OrderBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolvePluginRoot(string pluginDirectory)
    {
        if (Path.IsPathRooted(pluginDirectory))
        {
            return Path.GetFullPath(pluginDirectory);
        }

        return Path.GetFullPath(pluginDirectory, AppContext.BaseDirectory);
    }

    private static GopherPluginManifest ReadManifest(string manifestPath)
    {
        try
        {
            string json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<GopherPluginManifest>(json, JsonOptions)
                ?? throw new InvalidDataException($"Gopher plugin manifest '{manifestPath}' was empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Gopher plugin manifest '{manifestPath}' contains invalid JSON.",
                ex);
        }
    }

    private static void ValidateManifest(
        GopherPluginManifest manifest,
        string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException(
                $"Gopher plugin manifest '{manifestPath}' " +
                "must specify a non-empty plugin id.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidDataException(
                $"Gopher plugin manifest '{manifestPath}' " +
                "must specify a non-empty entry assembly.");
        }
    }

    private static void EnsurePathIsInsidePlugin(
        string pluginDirectory,
        string entryAssemblyPath,
        string pluginId)
    {
        string relativePath = Path.GetRelativePath(pluginDirectory, entryAssemblyPath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Entry assembly for Gopher plugin '{pluginId}' " +
                "must be contained within its plugin directory.");
        }
    }

    private static void EnsureUniquePluginIds(
        IEnumerable<GopherPluginDescriptor> plugins)
    {
        HashSet<string> pluginIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (GopherPluginDescriptor plugin in plugins)
        {
            if (!pluginIds.Add(plugin.Id))
            {
                throw new InvalidDataException($"Duplicate Gopher plugin id '{plugin.Id}'.");
            }
        }
    }
}