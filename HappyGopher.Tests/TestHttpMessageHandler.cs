using System.Net;

namespace HappyGopher.Tests;

internal sealed class TestHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
        responseFactory) : HttpMessageHandler
{
    public HttpMethod? LastMethod { get; private set; }
    public Uri? LastRequestUri { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public int RequestCount { get; private set; }

    public static TestHttpMessageHandler RespondWith(
        HttpStatusCode statusCode,
        string? body = null) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = body is null
                ? null
                : new StringContent(body)
        }));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastMethod = request.Method;
        LastRequestUri = request.RequestUri;
        LastCancellationToken = cancellationToken;
        return responseFactory(request, cancellationToken);
    }
}
