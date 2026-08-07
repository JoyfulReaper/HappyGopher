/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Extensibility;

/// <summary>
/// Represents one Gopher request line.
/// </summary>
public sealed record GopherRequest(string Selector, string? Input);