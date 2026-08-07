using HappyGopher.Abstractions;
using HappyGopher.Integrations.HappyQotd;
using HappyGopher.Pages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HappyGopher.Tests;

public sealed class HappyQotdServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHappyQotd_RegistersClientAndPagesWhenEnabled()
    {
        IConfiguration configuration = CreateConfiguration(
            enabled: true,
            baseUrl: "https://qotd.test",
            timeoutMilliseconds: 1500);
        ServiceCollection services = new();
        services.AddLogging();

        services.AddHappyQotd(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<HappyQotdClient>(
            provider.GetRequiredService<IHappyQotdClient>());
        IGopherPage[] pages = provider
            .GetServices<IGopherPage>()
            .ToArray();
        Assert.Contains(
            pages,
            page => page is QuoteOfTheDayPage && page.Selector == "/qotd");
        Assert.Contains(
            pages,
            page => page is RandomQuotePage && page.Selector == "/random-quote");
    }

    [Fact]
    public void AddHappyQotd_DoesNotRegisterClientOrPagesWhenDisabled()
    {
        IConfiguration configuration = CreateConfiguration(
            enabled: false,
            baseUrl: "https://qotd.test",
            timeoutMilliseconds: 1500);
        ServiceCollection services = new();
        services.AddLogging();

        services.AddHappyQotd(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IHappyQotdClient>());
        Assert.DoesNotContain(
            provider.GetServices<IGopherPage>(),
            page => page is QuoteOfTheDayPage or RandomQuotePage);
    }

    [Fact]
    public async Task AddHappyQotd_RejectsRelativeBaseUrlAtStartup()
    {
        await AssertStartupValidationFailsAsync(
            baseUrl: "relative/path",
            timeoutMilliseconds: 1500,
            "HappyQotd:BaseUrl must be an absolute URI");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddHappyQotd_RejectsNonPositiveTimeoutAtStartup(
        int timeoutMilliseconds)
    {
        await AssertStartupValidationFailsAsync(
            baseUrl: "https://qotd.test",
            timeoutMilliseconds,
            "HappyQotd:TimeoutMilliseconds must be positive");
    }

    private static async Task AssertStartupValidationFailsAsync(
        string baseUrl,
        int timeoutMilliseconds,
        string expectedFailure)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            CreateSettings(
                enabled: true,
                baseUrl,
                timeoutMilliseconds));
        builder.Services.AddHappyQotd(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception =
            await Assert.ThrowsAsync<OptionsValidationException>(
                () => host.StartAsync());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(
                expectedFailure,
                StringComparison.Ordinal));
    }

    private static IConfiguration CreateConfiguration(
        bool enabled,
        string baseUrl,
        int timeoutMilliseconds) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                CreateSettings(enabled, baseUrl, timeoutMilliseconds))
            .Build();

    private static Dictionary<string, string?> CreateSettings(
        bool enabled,
        string baseUrl,
        int timeoutMilliseconds) =>
        new()
        {
            [$"{HappyQotdOptions.SectionName}:Enabled"] =
                enabled.ToString(),
            [$"{HappyQotdOptions.SectionName}:BaseUrl"] = baseUrl,
            [$"{HappyQotdOptions.SectionName}:TimeoutMilliseconds"] =
                timeoutMilliseconds.ToString()
        };
}
