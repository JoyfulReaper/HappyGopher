/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher;

public sealed class HappyGopherOptions
{
    public const string SectionName = "Gopher";
    public string ListenAddress { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 70;
    public string PublicHost { get; init; } = "localhost";
    public string ContentRoot { get; init; } = "content";
    public int MaxConcurrentConnections { get; init; } = 64;
    public int MaxSelectorBytes { get; init; } = 4096;
    public int RequestTimeoutSeconds { get; init; } = 15;
}
