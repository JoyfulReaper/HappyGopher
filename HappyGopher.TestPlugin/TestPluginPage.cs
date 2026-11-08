using HappyGopher.Extensibility;

namespace HappyGopher.TestPlugin;

[AutoRegisterGopherPage]
public sealed class TestPluginPage : IGopherPage
{
    public TestPluginPage(TestPluginDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
    }

    public string Selector => "/test-plugin";

    public async Task<GopherResponseKind> WriteAsync(
        GopherRequest request,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using GopherResponseWriter writer = new(output);

        await writer.WriteTextLineAsync(
            "PLUGIN WORKS",
            cancellationToken);

        await writer.CompleteAsync(
            cancellationToken);

        return GopherResponseKind.Text;
    }
}