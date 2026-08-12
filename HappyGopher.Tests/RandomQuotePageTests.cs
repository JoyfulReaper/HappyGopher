using HappyGopher.Extensibility;
using HappyGopher.Integrations.HappyQotd;
using HappyGopher.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;

namespace HappyGopher.Tests;

public sealed class RandomQuotePageTests
{
    [Fact]
    public async Task WriteAsync_WritesCompletedQuoteResponse()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(
                    1,
                    "Stay curious.",
                    "Grace Hopper",
                    "Collected wisdom"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote\r\n\r\nStay curious.\r\n" +
            "-- Grace Hopper\r\nSource: Collected wisdom\r\n.\r\n",
            response);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task WriteAsync_OmitsNullOrBlankAuthor(string? author)
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(1, "Quote", author, "Source"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.DoesNotContain("-- ", response);
        Assert.Contains("Source: Source\r\n", response);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task WriteAsync_OmitsNullOrBlankSource(string? source)
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(1, "Quote", "Author", source))
        });

        string response = await WriteCompletedAsync(page);

        Assert.DoesNotContain("Source:", response);
        Assert.Contains("-- Author\r\n", response);
    }

    [Fact]
    public async Task WriteAsync_NormalizesMultilineQuoteToCrlf()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(
                    1,
                    "First\rSecond\nThird\r\nFourth"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote\r\n\r\n" +
            "First\r\nSecond\r\nThird\r\nFourth\r\n.\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_DotStuffsQuoteLines()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(1, ".Hidden line"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Contains("\r\n..Hidden line\r\n", response);
    }

    [Fact]
    public async Task WriteAsync_WritesNoQuoteMessageForNullResult()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient());

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote\r\n\r\nNo quote was returned\r\n.\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_WritesUnavailableResponseForHttpFailure()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ =>
                Task.FromException<HappyQotdQuote?>(
                    new HttpRequestException("Unavailable"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote is temporarily unavailable.\r\n.\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_WritesTimeoutResponseForUpstreamCancellation()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ =>
                Task.FromException<HappyQotdQuote?>(
                    new TaskCanceledException("Timed out"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote request timed out.\r\n.\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_PropagatesCallerRequestedCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = cancellationToken =>
                Task.FromCanceled<HappyQotdQuote?>(cancellationToken)
        });
        await using MemoryStream output = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            page.WriteAsync(
                new GopherRequest(page.Selector, Input: null),
                output,
                cancellation.Token));

        Assert.DoesNotContain(
            "request timed out",
            Encoding.UTF8.GetString(output.ToArray()),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteAsync_WritesUnavailableResponseForMalformedJson()
    {
        RecordingLogger<RandomQuotePage> logger = new();
        RandomQuotePage page = CreatePage(
            new StubHappyQotdClient
            {
                RandomQuote = _ =>
                    Task.FromException<HappyQotdQuote?>(
                        new JsonException("Malformed"))
            },
            logger);

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote is temporarily unavailable.\r\n.\r\n",
            response);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    [Fact]
    public async Task WriteAsync_WritesUnavailableResponseForNullQuoteText()
    {
        RecordingLogger<RandomQuotePage> logger = new();
        RandomQuotePage page = CreatePage(
            new StubHappyQotdClient
            {
                RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                    new HappyQotdQuote(1, null!))
            },
            logger);

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            "Random Quote is temporarily unavailable.\r\n.\r\n",
            response);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    private static RandomQuotePage CreatePage(
        IHappyQotdClient client,
        ILogger<RandomQuotePage>? logger = null) =>
        new(client, logger ?? NullLogger<RandomQuotePage>.Instance);

    private static async Task<string> WriteCompletedAsync(
        RandomQuotePage page)
    {
        await using MemoryStream output = new();

        GopherResponseKind responseKind = await page.WriteAsync(
            new GopherRequest(page.Selector, Input: null),
            output,
            CancellationToken.None);
        string response = Encoding.UTF8.GetString(output.ToArray());

        Assert.Equal(GopherResponseKind.Text, responseKind);
        Assert.EndsWith(".\r\n", response);
        return response;
    }
}
