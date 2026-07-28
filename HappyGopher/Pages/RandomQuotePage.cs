/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;
using HappyGopher.Integrations.HappyQotd;

namespace HappyGopher.Pages;

public sealed class RandomQuotePage(
    IHappyQotdClient happyQotdClient,
    ILogger<RandomQuotePage> logger) : IGopherPage
{
    public const string PageSelector = "/random-quote";

    public string Selector => PageSelector;

    public async Task<GopherResponseKind> WriteAsync(
        GopherRequest request,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        await using GopherResponseWriter writer = new(output);

        try
        {
            HappyQotdQuote? quote = await happyQotdClient.GetRandomQuoteAsync(cancellationToken);

            await writer.WriteTextLineAsync("Random Quote", cancellationToken);
            await writer.WriteTextLineAsync(string.Empty, cancellationToken);

            if (quote is null)
            {
                await writer.WriteTextLineAsync(
                    "No quote was returned",
                    cancellationToken);
            }
            else
            {
                foreach (string line in SplitLines(quote.Text))
                {
                    await writer.WriteTextLineAsync(line, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(quote.Author))
                {
                    await writer.WriteTextLineAsync($"-- {quote.Author}", cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(quote.Source))
                {
                    await writer.WriteTextLineAsync($"Source: {quote.Source}", cancellationToken);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "HappyQOTD was unavailable while serving {Selector}.",
                Selector);

            await writer.WriteTextLineAsync(
                "Random Quote is temporarily unavailable.",
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "HappyQOTD timed out while serving {Selector}.",
                Selector);

            await writer.WriteTextLineAsync(
                "Random Quote request timed out.",
                cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Text;
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.ReplaceLineEndings("\n").Split('\n');
}