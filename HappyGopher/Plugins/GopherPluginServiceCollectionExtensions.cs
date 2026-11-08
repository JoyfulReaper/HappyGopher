/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using HappyGopher.Pages;
using System.Reflection;

namespace HappyGopher.Plugins;

/// <summary>
/// Registers external HappyGopher plugins and their pages.
/// </summary>
public static class GopherPluginServiceCollectionExtensions
{
    public static IServiceCollection AddGopherPlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        GopherPluginOptions options = configuration
            .GetSection(GopherPluginOptions.SectionName)
            .Get<GopherPluginOptions>()
            ?? new GopherPluginOptions();

        if (string.IsNullOrWhiteSpace(options.PluginDirectory))
        {
            throw new InvalidOperationException(
                $"{GopherPluginOptions.SectionName}:PluginDirectory " +
                "must not be empty.");
        }

        GopherPluginDiscovery discovery = new();
        GopherPluginLoader loader = new();

        IReadOnlyList<GopherPluginDescriptor> descriptors = discovery.Discover(options.PluginDirectory);

        foreach (GopherPluginDescriptor descriptor in descriptors)
        {
            GopherLoadedPlugin loaded = loader.Load(descriptor);

            ConfigurePluginServices(
                loaded,
                services,
                configuration);

            services.AddGopherPagesFromAssemblies(
                loaded.EntryAssembly);

            // Retain metadata about loaded plugins for diagnostics
            // and future status/reporting features.
            services.AddSingleton(loaded);
        }

        return services;
    }

    private static void ConfigurePluginServices(
        GopherLoadedPlugin loaded,
        IServiceCollection services,
        IConfiguration configuration)
    {
        IEnumerable<TypeInfo> pluginTypes =
            loaded.EntryAssembly.DefinedTypes
                .Where(type =>
                    type.IsPublic &&
                    type.IsClass &&
                    !type.IsAbstract &&
                    !type.ContainsGenericParameters &&
                    typeof(IHappyGopherPlugin)
                        .IsAssignableFrom(type))
                .OrderBy(
                    type => type.FullName ?? type.Name,
                    StringComparer.Ordinal);

        foreach (TypeInfo pluginType in pluginTypes)
        {
            if (Activator.CreateInstance(pluginType.AsType())
                is not IHappyGopherPlugin plugin)
            {
                throw new InvalidOperationException(
                    $"Could not create plugin entry point " +
                    $"'{pluginType.FullName}' for " +
                    $"'{loaded.Descriptor.Id}'.");
            }

            plugin.ConfigureServices(
                services,
                configuration);
        }
    }
}