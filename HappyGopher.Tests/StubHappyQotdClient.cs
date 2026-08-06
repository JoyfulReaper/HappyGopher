using HappyGopher.Integrations.HappyQotd;

namespace HappyGopher.Tests;

internal sealed class StubHappyQotdClient : IHappyQotdClient
{
    public Func<CancellationToken, Task<HappyQotdQuote?>> QuoteOfTheDay { get; init; } =
        _ => Task.FromResult<HappyQotdQuote?>(null);

    public Func<CancellationToken, Task<HappyQotdQuote?>> RandomQuote { get; init; } =
        _ => Task.FromResult<HappyQotdQuote?>(null);

    public Task<HappyQotdQuote?> GetQuoteOfTheDayAsync(
        CancellationToken cancellationToken = default) =>
        QuoteOfTheDay(cancellationToken);

    public Task<HappyQotdQuote?> GetRandomQuoteAsync(
        CancellationToken cancellationToken = default) =>
        RandomQuote(cancellationToken);
}
