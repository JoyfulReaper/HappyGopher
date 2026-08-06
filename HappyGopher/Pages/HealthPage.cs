/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;

namespace HappyGopher.Pages;

/// <summary>
/// Provides a stable operational health response without external dependencies.
/// </summary>
public sealed class HealthPage : IGopherPage
{
    public const string PageSelector = "/healthz";

    public string Selector => PageSelector;

    public async Task<GopherResponseKind> WriteAsync(
        GopherRequest request,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        await using GopherResponseWriter writer = new(output);
        await writer.WriteTextLineAsync("OK", cancellationToken);
        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Text;
    }
}
