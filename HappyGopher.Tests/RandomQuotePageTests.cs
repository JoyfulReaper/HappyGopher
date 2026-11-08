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
    private const string MainMenu = "1Go back to main menu\t\tlocalhost\t70\r\n";
    private const string AnotherQuote = "1Get another quote\t/random-quote\tlocalhost\t70\r\n";

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
            Info("Random Quote") + Info(string.Empty) + Info("Stay curious.") +
            Info("-- Grace Hopper") + Info("Source: Collected wisdom") +
            Info(string.Empty) + AnotherQuote + MainMenu + ".\r\n",
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
        Assert.Contains(Info("Source: Source"), response);
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
        Assert.Contains(Info("-- Author"), response);
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
            Info("Random Quote") + Info(string.Empty) +
            Info("First") + Info("Second") + Info("Third") + Info("Fourth") +
            Info(string.Empty) + AnotherQuote + MainMenu + ".\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_WritesLeadingDotQuoteAsInfoItem()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient
        {
            RandomQuote = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(1, ".Hidden line"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Contains("\r\n" + Info(".Hidden line"), response);
    }

    [Fact]
    public async Task WriteAsync_WritesNoQuoteMessageForNullResult()
    {
        RandomQuotePage page = CreatePage(new StubHappyQotdClient());

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Random Quote") + Info(string.Empty) + Info("No quote was returned") +
            Info(string.Empty) + AnotherQuote + MainMenu + ".\r\n",
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
            Info("Random Quote is temporarily unavailable.") + ".\r\n",
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
            Info("Random Quote request timed out.") + ".\r\n",
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
            Info("Random Quote is temporarily unavailable.") + ".\r\n",
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
            Info("Random Quote is temporarily unavailable.") + ".\r\n",
            response);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    private static string Info(string text) =>
        $"i{text}\tfake\t(NULL)\t0\r\n";

    private static RandomQuotePage CreatePage(
         IHappyQotdClient client,
         ILogger<RandomQuotePage>? logger = null) =>
         new(
             client,
             new GopherServerInfo("localhost", 70),
             logger ?? NullLogger<RandomQuotePage>.Instance);

    private static async Task<string> WriteCompletedAsync(
        RandomQuotePage page)
    {
        await using MemoryStream output = new();

        GopherResponseKind responseKind = await page.WriteAsync(
            new GopherRequest(page.Selector, Input: null),
            output,
            CancellationToken.None);
        string response = Encoding.UTF8.GetString(output.ToArray());

        Assert.Equal(GopherResponseKind.Menu, responseKind);
        Assert.EndsWith(".\r\n", response);
        return response;
    }
}
