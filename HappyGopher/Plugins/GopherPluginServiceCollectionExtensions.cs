/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Pages;

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

            services.AddGopherPagesFromAssemblies(loaded.EntryAssembly);

            // Retain metadata about loaded plugins for diagnostics
            // and future status/reporting features.
            services.AddSingleton(loaded);
        }

        return services;
    }
}