/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Plugins;

namespace HappyGopher.Tests;

public sealed class GopherPluginDiscoveryTests
{
    [Fact]
    public void Discover_MissingPluginDirectory_ReturnsEmpty()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("does-not-exist");

        GopherPluginDiscovery discovery = new();

        IReadOnlyList<GopherPluginDescriptor> plugins =
            discovery.Discover(pluginDirectory);

        Assert.Empty(plugins);
    }

    [Fact]
    public void Discover_DirectoryWithoutManifest_IsIgnored()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        Directory.CreateDirectory(
            Path.Combine(pluginDirectory, "not-a-plugin"));

        GopherPluginDiscovery discovery = new();

        IReadOnlyList<GopherPluginDescriptor> plugins =
            discovery.Discover(pluginDirectory);

        Assert.Empty(plugins);
    }

    [Fact]
    public void Discover_ValidPlugin_ReturnsDescriptor()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        string pluginPath = CreatePlugin(
            pluginDirectory,
            directoryName: "mystery",
            id: "JoyfulReaper.MysteryPages",
            entryAssembly: "HappyGopher.CustomPages.dll");

        string expectedAssemblyPath = Path.Combine(
            pluginPath,
            "HappyGopher.CustomPages.dll");

        GopherPluginDiscovery discovery = new();

        IReadOnlyList<GopherPluginDescriptor> plugins =
            discovery.Discover(pluginDirectory);

        GopherPluginDescriptor plugin =
            Assert.Single(plugins);

        Assert.Equal(
            "JoyfulReaper.MysteryPages",
            plugin.Id);

        Assert.Equal(
            Path.GetFullPath(pluginPath),
            plugin.DirectoryPath);

        Assert.Equal(
            Path.GetFullPath(expectedAssemblyPath),
            plugin.EntryAssemblyPath);
    }

    [Fact]
    public void Discover_InvalidJson_Throws()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        string pluginPath =
            Path.Combine(pluginDirectory, "broken");

        Directory.CreateDirectory(pluginPath);

        File.WriteAllText(
            Path.Combine(
                pluginPath,
                "happygopher.plugin.json"),
            "{ nope");

        GopherPluginDiscovery discovery = new();

        Assert.Throws<InvalidDataException>(
            () => discovery.Discover(pluginDirectory));
    }

    [Fact]
    public void Discover_BlankId_Throws()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        CreatePlugin(
            pluginDirectory,
            directoryName: "broken",
            id: " ",
            entryAssembly: "Plugin.dll");

        GopherPluginDiscovery discovery = new();

        Assert.Throws<InvalidDataException>(
            () => discovery.Discover(pluginDirectory));
    }

    [Fact]
    public void Discover_BlankEntryAssembly_Throws()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        string pluginPath =
            Path.Combine(pluginDirectory, "broken");

        Directory.CreateDirectory(pluginPath);

        File.WriteAllText(
            Path.Combine(
                pluginPath,
                "happygopher.plugin.json"),
            """
            {
              "id": "JoyfulReaper.Broken",
              "entryAssembly": ""
            }
            """);

        GopherPluginDiscovery discovery = new();

        Assert.Throws<InvalidDataException>(
            () => discovery.Discover(pluginDirectory));
    }

    [Fact]
    public void Discover_MissingEntryAssembly_Throws()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        string pluginPath =
            Path.Combine(pluginDirectory, "broken");

        Directory.CreateDirectory(pluginPath);

        File.WriteAllText(
            Path.Combine(
                pluginPath,
                "happygopher.plugin.json"),
            """
            {
              "id": "JoyfulReaper.Broken",
              "entryAssembly": "Missing.dll"
            }
            """);

        GopherPluginDiscovery discovery = new();

        Assert.Throws<FileNotFoundException>(
            () => discovery.Discover(pluginDirectory));
    }

    [Fact]
    public void Discover_EntryAssemblyOutsidePluginDirectory_Throws()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        string pluginPath =
            Path.Combine(pluginDirectory, "mystery");

        string otherPluginPath =
            Path.Combine(pluginDirectory, "other");

        Directory.CreateDirectory(pluginPath);
        Directory.CreateDirectory(otherPluginPath);

        File.WriteAllBytes(
            Path.Combine(otherPluginPath, "Other.dll"),
            []);

        File.WriteAllText(
            Path.Combine(
                pluginPath,
                "happygopher.plugin.json"),
            """
            {
              "id": "JoyfulReaper.MysteryPages",
              "entryAssembly": "../other/Other.dll"
            }
            """);

        GopherPluginDiscovery discovery = new();

        Assert.Throws<InvalidDataException>(
            () => discovery.Discover(pluginDirectory));
    }

    [Fact]
    public void Discover_DuplicatePluginIds_Throws()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        CreatePlugin(
            pluginDirectory,
            directoryName: "first",
            id: "JoyfulReaper.MysteryPages",
            entryAssembly: "First.dll");

        CreatePlugin(
            pluginDirectory,
            directoryName: "second",
            id: "joyfulreaper.mysterypages",
            entryAssembly: "Second.dll");

        GopherPluginDiscovery discovery = new();

        Assert.Throws<InvalidDataException>(
            () => discovery.Discover(pluginDirectory));
    }

    [Fact]
    public void Discover_ReturnsPluginsInDeterministicOrder()
    {
        using TemporaryDirectory temp = new();

        string pluginDirectory =
            temp.GetPath("plugins");

        CreatePlugin(
            pluginDirectory,
            directoryName: "zulu",
            id: "Plugin.Zulu",
            entryAssembly: "Zulu.dll");

        CreatePlugin(
            pluginDirectory,
            directoryName: "alpha",
            id: "Plugin.Alpha",
            entryAssembly: "Alpha.dll");

        CreatePlugin(
            pluginDirectory,
            directoryName: "middle",
            id: "Plugin.Middle",
            entryAssembly: "Middle.dll");

        GopherPluginDiscovery discovery = new();

        IReadOnlyList<GopherPluginDescriptor> plugins =
            discovery.Discover(pluginDirectory);

        Assert.Collection(
            plugins,
            plugin => Assert.Equal(
                "Plugin.Alpha",
                plugin.Id),
            plugin => Assert.Equal(
                "Plugin.Middle",
                plugin.Id),
            plugin => Assert.Equal(
                "Plugin.Zulu",
                plugin.Id));
    }

    private static string CreatePlugin(
        string pluginDirectory,
        string directoryName,
        string id,
        string entryAssembly)
    {
        string pluginPath =
            Path.Combine(pluginDirectory, directoryName);

        Directory.CreateDirectory(pluginPath);

        File.WriteAllText(
            Path.Combine(
                pluginPath,
                "happygopher.plugin.json"),
            $$"""
            {
              "id": "{{id}}",
              "entryAssembly": "{{entryAssembly}}"
            }
            """);

        if (!string.IsNullOrWhiteSpace(entryAssembly))
        {
            string assemblyPath =
                Path.Combine(pluginPath, entryAssembly);

            string? assemblyDirectory =
                Path.GetDirectoryName(assemblyPath);

            if (assemblyDirectory is not null)
            {
                Directory.CreateDirectory(assemblyDirectory);
            }

            File.WriteAllBytes(
                assemblyPath,
                []);
        }

        return pluginPath;
    }
}