namespace HappyGopher.Plugins;

public sealed class GopherPluginOptions
{
    public const string SectionName = "GopherPages";

    public string PluginDirectory { get; init; } = "plugins";
}