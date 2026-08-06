using HappyGopher.Integrations.HappyQotd;
using System.Net;
using System.Text.Json;

namespace HappyGopher.Tests;

public sealed class HappyQotdClientTests
{
    private const string CompleteQuoteJson =
        """
        {
          "id": 42,
          "text": "Stay curious.",
          "author": "Grace Hopper",
          "source": "Collected wisdom"
        }
        """;

    [Fact]
    public async Task GetQuoteOfTheDayAsync_RequestsTodayEndpointAndDeserializesQuote()
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(
                HttpStatusCode.OK,
                CompleteQuoteJson);
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        HappyQotdQuote? quote = await client.GetQuoteOfTheDayAsync();

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal(
            new Uri("https://qotd.test/api/quotes/today"),
            handler.LastRequestUri);
        AssertQuote(quote);
    }

    [Fact]
    public async Task GetRandomQuoteAsync_RequestsRandomEndpointAndDeserializesQuote()
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(
                HttpStatusCode.OK,
                CompleteQuoteJson);
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        HappyQotdQuote? quote = await client.GetRandomQuoteAsync();

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal(
            new Uri("https://qotd.test/api/quotes/random"),
            handler.LastRequestUri);
        AssertQuote(quote);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetQuoteAsync_ReturnsNullForNotFound(bool random)
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(HttpStatusCode.NotFound);
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        HappyQotdQuote? quote = await GetQuoteAsync(client, random);

        Assert.Null(quote);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetQuoteAsync_ThrowsForUnsuccessfulStatus(bool random)
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(
                HttpStatusCode.InternalServerError);
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => GetQuoteAsync(client, random));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetQuoteAsync_PropagatesCancellation(bool random)
    {
        TaskCompletionSource requestStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TestHttpMessageHandler handler = new(async (_, cancellationToken) =>
        {
            requestStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);
        using CancellationTokenSource cancellation = new();

        Task<HappyQotdQuote?> request =
            GetQuoteAsync(client, random, cancellation.Token);
        await requestStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(handler.LastCancellationToken.CanBeCanceled);
        Assert.True(handler.LastCancellationToken.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetQuoteAsync_ThrowsForMalformedJson(bool random)
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(
                HttpStatusCode.OK,
                "{not valid JSON}");
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        await Assert.ThrowsAsync<JsonException>(
            () => GetQuoteAsync(client, random));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetQuoteAsync_DeserializesNullOptionalFields(bool random)
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(
                HttpStatusCode.OK,
                """
                { "id": 7, "text": "Anonymous wisdom", "author": null, "source": null }
                """);
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        HappyQotdQuote? quote = await GetQuoteAsync(client, random);

        Assert.NotNull(quote);
        Assert.Equal(7, quote.Id);
        Assert.Equal("Anonymous wisdom", quote.Text);
        Assert.Null(quote.Author);
        Assert.Null(quote.Source);
    }

    [Fact]
    public async Task Requests_DoNotDisposeOrCorruptInjectedHttpClient()
    {
        TestHttpMessageHandler handler =
            TestHttpMessageHandler.RespondWith(
                HttpStatusCode.OK,
                CompleteQuoteJson);
        using HttpClient httpClient = CreateHttpClient(handler);
        HappyQotdClient client = new(httpClient);

        await client.GetQuoteOfTheDayAsync();
        await client.GetRandomQuoteAsync();

        Assert.Equal(2, handler.RequestCount);
    }

    private static HttpClient CreateHttpClient(
        HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("https://qotd.test/")
        };

    private static Task<HappyQotdQuote?> GetQuoteAsync(
        HappyQotdClient client,
        bool random,
        CancellationToken cancellationToken = default) =>
        random
            ? client.GetRandomQuoteAsync(cancellationToken)
            : client.GetQuoteOfTheDayAsync(cancellationToken);

    private static void AssertQuote(HappyQotdQuote? quote)
    {
        Assert.NotNull(quote);
        Assert.Equal(42, quote.Id);
        Assert.Equal("Stay curious.", quote.Text);
        Assert.Equal("Grace Hopper", quote.Author);
        Assert.Equal("Collected wisdom", quote.Source);
    }
}
