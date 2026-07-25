/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Pages;

/// <summary>
/// Represents a dynamic Gopher resource registered for an exact selector.
/// </summary>
public interface IGopherPage
{
    /// <summary>
    /// Gets the selector handled by this page.
    /// </summary>
    string Selector { get; }

    /// <summary>
    /// Writes the Gopher response to the supplied output stream.
    /// </summary>
    Task<GopherResponseKind> WriteAsync(
        Stream output,
        CancellationToken cancellationToken);
}