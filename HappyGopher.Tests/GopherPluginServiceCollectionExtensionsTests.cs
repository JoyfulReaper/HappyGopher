using HappyGopher.Extensibility;
using HappyGopher.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGopher.Tests;

public sealed class GopherPluginServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGopherPlugins_LoadsAndRegistersPlugin()
    {
        IConfiguration configuration =
            CreateConfiguration(
                AppContext.BaseDirectory);

        ServiceCollection services = new();

        services.AddGopherPlugins(configuration);

        using ServiceProvider provider =
            services.BuildServiceProvider();

        GopherLoadedPlugin loadedPlugin =
            Assert.Single(
                provider.GetServices<GopherLoadedPlugin>());

        Assert.Equal(
            "HappyGopher.Tests.TestPlugin",
            loadedPlugin.Descriptor.Id);

        IGopherPage page =
            Assert.Single(
                provider.GetServices<IGopherPage>());

        Assert.Equal(
            "/test-plugin",
            page.Selector);
    }

    [Fact]
    public void AddGopherPlugins_MissingDirectory_RegistersNothing()
    {
        using TemporaryDirectory temp = new();

        string missingDirectory =
            temp.GetPath("does-not-exist");

        IConfiguration configuration =
            CreateConfiguration(
                missingDirectory);

        ServiceCollection services = new();

        services.AddGopherPlugins(configuration);

        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IGopherPage));

        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(GopherLoadedPlugin));
    }

    private static IConfiguration CreateConfiguration(
        string pluginDirectory)
    {
        Dictionary<string, string?> values = new()
        {
            [$"{GopherPluginOptions.SectionName}:PluginDirectory"] =
                pluginDirectory
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}