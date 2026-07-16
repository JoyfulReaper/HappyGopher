/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher;

public sealed class HappyGopherOptions
{
    public const string SectionName = "Gopher";
    public string ListenAddress { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 70;
    public string PublicHost { get; init; } = "127.0.0.1";
    public string ContentRoot { get; init; } = "content";
    public int MaxConcurrentConnections { get; init; } = 64;
    public int MaxSelectorBytes { get; init; } = 4096;
    public int RequestTimeoutSeconds { get; init; } = 15;
    public string? TelemetryIgnoredRemoteAddress { get; init; }
}
