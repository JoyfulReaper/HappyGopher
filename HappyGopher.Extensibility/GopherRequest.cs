/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Net;

namespace HappyGopher.Extensibility;

/// <summary>
/// Represents one Gopher request and its connection metadata.
/// </summary>
public sealed record GopherRequest(
    string Selector,
    string? Input)
{
    /// <summary>
    /// Gets the remote client endpoint, when available.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the local server endpoint that accepted the connection,
    /// including the destination address used by the client.
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; init; }
}