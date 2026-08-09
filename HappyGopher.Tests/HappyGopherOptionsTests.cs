using HappyGopher.Gopher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HappyGopher.Tests;

public sealed class HappyGopherOptionsTests
{
    [Fact]
    public void ResponseTimeoutSeconds_DefaultsToSixtySeconds()
    {
        HappyGopherOptions options = new();

        Assert.Equal(60, options.ResponseTimeoutSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ResponseTimeoutValidation_RejectsNonPositiveValue(
        int responseTimeoutSeconds)
    {
        ServiceCollection services = new();
        services.AddOptions<HappyGopherOptions>()
            .Configure(options =>
                options.ResponseTimeoutSeconds =
                    responseTimeoutSeconds)
            .ValidateResponseTimeout();

        using ServiceProvider provider =
            services.BuildServiceProvider();

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(() =>
                provider
                    .GetRequiredService<IOptions<HappyGopherOptions>>()
                    .Value);

        Assert.Contains(
            "Gopher:ResponseTimeoutSeconds must be positive.",
            exception.Failures);
    }
}
