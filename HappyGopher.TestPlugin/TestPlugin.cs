using HappyGopher.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGopher.TestPlugin;

public sealed class TestPlugin : IHappyGopherPlugin
{
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<TestPluginDependency>();
    }
}

public sealed class TestPluginDependency;