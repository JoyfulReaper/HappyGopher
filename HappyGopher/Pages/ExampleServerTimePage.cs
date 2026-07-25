/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;
using System.Globalization;

namespace HappyGopher.Pages;

/// <summary>
/// Displays the current server time in UTC.
/// </summary>
public sealed class ServerTimePage(TimeProvider timeProvider) : IGopherPage
{
    public const string PageSelector = "/server-time";

    public string Selector =>
        PageSelector;

    public async Task<GopherResponseKind> WriteAsync(
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        await using GopherResponseWriter writer = new(output);

        await writer.WriteTextLineAsync("HappyGopher server time", cancellationToken);

        string currentTime = timeProvider
            .GetUtcNow()
            .ToString("O", CultureInfo.InvariantCulture);

        await writer.WriteTextLineAsync($"UTC: {currentTime}", cancellationToken);
        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Text;
    }
}