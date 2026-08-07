namespace HappyGopher.Plugins;

public sealed record GopherPluginManifest
{
    public required string Id { get; init; }

    public required string EntryAssembly { get; init; }
}