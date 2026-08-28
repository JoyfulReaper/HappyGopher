/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Extensibility;

/// <summary>
/// Describes the public Gopher endpoint that plugins should use
/// when generating menu entries.
/// </summary>
public sealed record GopherServerInfo(
    string PublicHost,
    int Port);