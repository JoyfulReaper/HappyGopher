/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using HappyGopher.Integrations.HappyQotd;
using System.Text.Json;

namespace HappyGopher.Pages;

public sealed class RandomQuotePage(
    IHappyQotdClient happyQotdClient,
    GopherServerInfo serverInfo,
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

            if (quote is not null && quote.Text is null)
            {
                throw new JsonException(
                    "HappyQOTD returned a quote with null text.");
            }

            await writer.WriteInfoAsync("Random Quote", cancellationToken);
            await writer.WriteInfoAsync(string.Empty, cancellationToken);

            if (quote is null)
            {
                await writer.WriteInfoAsync(
                    "No quote was returned",
                    cancellationToken);
            }
            else
            {
                foreach (string line in SplitLines(quote.Text))
                {
                    await writer.WriteInfoAsync(line, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(quote.Author))
                {
                    await writer.WriteInfoAsync($"-- {quote.Author}", cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(quote.Source))
                {
                    await writer.WriteInfoAsync($"Source: {quote.Source}", cancellationToken);
                }
            }

            await writer.WriteInfoAsync(string.Empty, cancellationToken);
            await writer.WriteMenuItemAsync(
                type: '1',
                display: "Get another quote",
                selector: "/random-quote",
                host: serverInfo.PublicHost,
                port: serverInfo.Port,
                cancellationToken);
            await writer.WriteMenuItemAsync(
                type: '1',
                display: "Go back to main menu",
                selector: "",
                host: serverInfo.PublicHost,
                port: serverInfo.Port,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "HappyQOTD was unavailable while serving {Selector}.",
                Selector);

            await writer.WriteInfoAsync(
                "Random Quote is temporarily unavailable.",
                cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "HappyQOTD returned an invalid payload while serving {Selector}.",
                Selector);

            await writer.WriteInfoAsync(
                "Random Quote is temporarily unavailable.",
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "HappyQOTD timed out while serving {Selector}.",
                Selector);

            await writer.WriteInfoAsync(
                "Random Quote request timed out.",
                cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Text;
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.ReplaceLineEndings("\n").Split('\n');
}
