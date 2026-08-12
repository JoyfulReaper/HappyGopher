using Microsoft.Extensions.Options;

namespace HappyGopher.Gopher;

internal static class HappyGopherOptionsBuilderExtensions
{
    public static OptionsBuilder<HappyGopherOptions> ValidateResponseTimeout(
        this OptionsBuilder<HappyGopherOptions> builder) =>
        builder.Validate(
            options => options.ResponseTimeoutSeconds > 0,
            "Gopher:ResponseTimeoutSeconds must be positive.");
}
