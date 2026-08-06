/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.TcpServer;

namespace HappyGopher.Gopher;

public sealed class HappyGopherOptions : ITcpServerOptions
{
    public const string SectionName = "Gopher";
    public string ListenAddress { get; set; } = "127.0.0.1";
    public bool DualMode { get; set; } = false;
    public int Port { get; set; } = 70;
    public string PublicHost { get; set; } = "127.0.0.1";
    public string ContentRoot { get; set; } = "content";
    public int MaxConcurrentConnections { get; set; } = 64;
    public int MaxSelectorBytes { get; set; } = 4096;
    public int MaxInputBytes { get; set; } = 1024;
    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the remote IP address used to suppress selector telemetry.
    /// When <see cref="TelemetryIgnoredSelectors"/> also contains configured
    /// selectors, both the remote address and selector must match.
    /// </summary>
    public string? TelemetryIgnoredRemoteAddress { get; set; }

    /// <summary>
    /// Gets or sets the exact, case-sensitive selectors used to suppress
    /// telemetry. When <see cref="TelemetryIgnoredRemoteAddress"/> is also
    /// configured, both the remote address and selector must match.
    /// </summary>
    public string[] TelemetryIgnoredSelectors { get; set; } = [];

    ConnectionLimitBehavior ITcpServerOptions.ConnectionLimitBehavior =>
        ConnectionLimitBehavior.Wait;
}
