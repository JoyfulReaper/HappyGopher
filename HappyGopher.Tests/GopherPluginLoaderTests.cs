using HappyGopher.Extensibility;
using HappyGopher.Pages;
using HappyGopher.Plugins;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Loader;
using System.Text;

namespace HappyGopher.Tests;

public sealed class GopherPluginLoaderTests
{
    [Fact]
    public void Load_LoadsPluginIntoDedicatedAssemblyLoadContext()
    {
        GopherPluginDescriptor descriptor =
            CreateTestPluginDescriptor();

        GopherPluginLoader loader = new();

        GopherLoadedPlugin loaded =
            loader.Load(descriptor);

        Assert.Equal(
            "HappyGopher.TestPlugin",
            loaded.EntryAssembly.GetName().Name);

        AssemblyLoadContext? loadContext =
            AssemblyLoadContext.GetLoadContext(
                loaded.EntryAssembly);

        Assert.NotNull(loadContext);
        Assert.NotSame(
            AssemblyLoadContext.Default,
            loadContext);

        Assert.Equal(
            "HappyGopher.Plugin:HappyGopher.Tests.TestPlugin",
            loadContext.Name);
    }

    [Fact]
    public void Load_PluginUsesHostExtensibilityContract()
    {
        GopherPluginDescriptor descriptor =
            CreateTestPluginDescriptor();

        GopherPluginLoader loader = new();

        GopherLoadedPlugin loaded =
            loader.Load(descriptor);

        Type pluginPage = Assert.Single(
            loaded.EntryAssembly.GetTypes(),
            type => type.FullName ==
                "HappyGopher.TestPlugin.TestPluginPage");

        Assert.True(
            typeof(IGopherPage)
                .IsAssignableFrom(pluginPage));
    }

    [Fact]
    public void LoadedPlugin_CanBeRegisteredByPageScanner()
    {
        GopherPluginDescriptor descriptor =
            CreateTestPluginDescriptor();

        GopherPluginLoader loader = new();

        GopherLoadedPlugin loaded =
            loader.Load(descriptor);

        Type pluginPage = Assert.Single(
            loaded.EntryAssembly.GetTypes(),
            type => type.FullName ==
                "HappyGopher.TestPlugin.TestPluginPage");

        ServiceCollection services = new();

        services.AddGopherPagesFromAssemblies(
            loaded.EntryAssembly);

        ServiceDescriptor registration =
            Assert.Single(
                services,
                service =>
                    service.ServiceType ==
                        typeof(IGopherPage) &&
                    service.ImplementationType ==
                        pluginPage);

        Assert.Equal(
            ServiceLifetime.Scoped,
            registration.Lifetime);
    }

    [Fact]
    public async Task LoadedPlugin_CanBeCreatedAndExecuted()
    {
        GopherPluginDescriptor descriptor =
            CreateTestPluginDescriptor();

        GopherPluginLoader loader = new();

        GopherLoadedPlugin loaded =
            loader.Load(descriptor);

        ServiceCollection services = new();

        services.AddGopherPagesFromAssemblies(
            loaded.EntryAssembly);

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        using IServiceScope scope =
            provider.CreateScope();

        IGopherPage page = Assert.Single(
            scope.ServiceProvider
                .GetServices<IGopherPage>());

        Assert.Equal(
            "/test-plugin",
            page.Selector);

        await using MemoryStream output = new();

        GopherResponseKind result =
            await page.WriteAsync(
                new GopherRequest(
                    "/test-plugin",
                    null),
                output,
                CancellationToken.None);

        Assert.Equal(
            GopherResponseKind.Text,
            result);

        Assert.Equal(
            "PLUGIN WORKS\r\n.\r\n",
            Encoding.UTF8.GetString(
                output.ToArray()));
    }

    [Fact]
    public void Load_InvalidAssembly_ThrowsUsefulException()
    {
        using TemporaryDirectory temp = new();

        string assemblyPath =
            temp.GetPath("Broken.dll");

        File.WriteAllText(
            assemblyPath,
            "this is definitely not a .NET assembly");

        GopherPluginDescriptor descriptor =
            new(
                "Broken.Plugin",
                temp.Path,
                assemblyPath);

        GopherPluginLoader loader = new();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => loader.Load(descriptor));

        Assert.Contains(
            "Broken.Plugin",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static GopherPluginDescriptor
        CreateTestPluginDescriptor()
    {
        string pluginDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "TestPlugin");

        string entryAssemblyPath =
            Path.Combine(
                pluginDirectory,
                "HappyGopher.TestPlugin.dll");

        Assert.True(
            File.Exists(entryAssemblyPath),
            $"Expected test plugin assembly at '{entryAssemblyPath}'.");

        return new GopherPluginDescriptor(
            "HappyGopher.Tests.TestPlugin",
            pluginDirectory,
            entryAssemblyPath);
    }
}