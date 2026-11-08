using HappyGopher.Extensibility;
using HappyGopher.Integrations.HappyQotd;
using HappyGopher.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;

namespace HappyGopher.Tests;

public sealed class QuoteOfTheDayPageTests
{
    private const string MainMenu = "1Go back to main menu\t\tlocalhost\t70\r\n";

    [Fact]
    public async Task WriteAsync_WritesCompletedQuoteResponseAndPreservesRequest()
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(
                    1,
                    "Stay curious.",
                    "Grace Hopper",
                    "Collected wisdom"))
        });
        GopherRequest request = new(page.Selector, Input: null);
        GopherRequest expectedRequest = request with { };

        string response = await WriteCompletedAsync(page, request);

        Assert.Equal(
            Info("Quote of the Day") + Info(string.Empty) + Info("Stay curious.") +
            Info("-- Grace Hopper") + Info("Source: Collected wisdom") +
            Info(string.Empty) + MainMenu + ".\r\n",
            response);
        Assert.Equal(expectedRequest, request);
        Assert.Null(request.Input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task WriteAsync_OmitsNullOrBlankAuthor(string? author)
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ => Task.FromResult<HappyQotdQuote?>(
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
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(1, "Quote", "Author", source))
        });

        string response = await WriteCompletedAsync(page);

        Assert.DoesNotContain("Source:", response);
        Assert.Contains(Info("-- Author"), response);
    }

    [Fact]
    public async Task WriteAsync_NormalizesMultilineQuoteToCrlf()
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(
                    1,
                    "First\rSecond\nThird\r\nFourth"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Quote of the Day") + Info(string.Empty) +
            Info("First") + Info("Second") + Info("Third") + Info("Fourth") +
            Info(string.Empty) + MainMenu + ".\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_WritesLeadingDotQuoteAsInfoItem()
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ => Task.FromResult<HappyQotdQuote?>(
                new HappyQotdQuote(1, ".Hidden line"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Contains("\r\n" + Info(".Hidden line"), response);
    }

    [Fact]
    public async Task WriteAsync_WritesNoQuoteMessageForNullResult()
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient());

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Quote of the Day") + Info(string.Empty) + Info("No quote has been selected for today.") +
            Info(string.Empty) + MainMenu + ".\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_WritesUnavailableResponseForHttpFailure()
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ =>
                Task.FromException<HappyQotdQuote?>(
                    new HttpRequestException("Unavailable"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Quote of the day is temporarily unavailable.") + ".\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_WritesTimeoutResponseForUpstreamCancellation()
    {
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = _ =>
                Task.FromException<HappyQotdQuote?>(
                    new TaskCanceledException("Timed out"))
        });

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Quote of the day request timed out.") + ".\r\n",
            response);
    }

    [Fact]
    public async Task WriteAsync_PropagatesCallerRequestedCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        QuoteOfTheDayPage page = CreatePage(new StubHappyQotdClient
        {
            QuoteOfTheDay = cancellationToken =>
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
        RecordingLogger<QuoteOfTheDayPage> logger = new();
        QuoteOfTheDayPage page = CreatePage(
            new StubHappyQotdClient
            {
                QuoteOfTheDay = _ =>
                    Task.FromException<HappyQotdQuote?>(
                        new JsonException("Malformed"))
            },
            logger);

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Quote of the day is temporarily unavailable.") + ".\r\n",
            response);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    [Fact]
    public async Task WriteAsync_WritesUnavailableResponseForNullQuoteText()
    {
        RecordingLogger<QuoteOfTheDayPage> logger = new();
        QuoteOfTheDayPage page = CreatePage(
            new StubHappyQotdClient
            {
                QuoteOfTheDay = _ => Task.FromResult<HappyQotdQuote?>(
                    new HappyQotdQuote(1, null!))
            },
            logger);

        string response = await WriteCompletedAsync(page);

        Assert.Equal(
            Info("Quote of the day is temporarily unavailable.") + ".\r\n",
            response);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    private static string Info(string text) =>
        $"i{text}\tfake\t(NULL)\t0\r\n";

    private static QuoteOfTheDayPage CreatePage(
        IHappyQotdClient client,
        ILogger<QuoteOfTheDayPage>? logger = null) =>
        new(
            client,
            new GopherServerInfo("localhost", 70),
            logger ?? NullLogger<QuoteOfTheDayPage>.Instance);

    private static Task<string> WriteCompletedAsync(
        QuoteOfTheDayPage page) =>
        WriteCompletedAsync(
            page,
            new GopherRequest(page.Selector, Input: null));

    private static async Task<string> WriteCompletedAsync(
        QuoteOfTheDayPage page,
        GopherRequest request)
    {
        await using MemoryStream output = new();

        GopherResponseKind responseKind = await page.WriteAsync(
            request,
            output,
            CancellationToken.None);
        string response = Encoding.UTF8.GetString(output.ToArray());

        Assert.Equal(GopherResponseKind.Menu, responseKind);
        Assert.EndsWith(".\r\n", response);
        return response;
    }
}
