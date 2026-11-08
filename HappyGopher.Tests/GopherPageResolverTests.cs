/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using HappyGopher.Pages;

namespace HappyGopher.Tests;

public sealed class GopherPageResolverTests
{
    [Fact]
    public void Resolve_ReturnsRegisteredPageForExactSelector()
    {
        StubPage page = new("/services/test");
        GopherPageResolver resolver = new([page]);

        IGopherPage? result =
            resolver.Resolve("/services/test");

        Assert.Same(page, result);
    }

    [Fact]
    public void Resolve_NormalizesMissingLeadingSlash()
    {
        StubPage page = new("services/test");
        GopherPageResolver resolver = new([page]);

        IGopherPage? result =
            resolver.Resolve("/services/test");

        Assert.Same(page, result);
    }

    [Fact]
    public void Resolve_NormalizesEmptySelectorToRoot()
    {
        StubPage page = new("/");
        GopherPageResolver resolver = new([page]);

        IGopherPage? result =
            resolver.Resolve(string.Empty);

        Assert.Same(page, result);
    }

    [Fact]
    public void Resolve_IsCaseSensitive()
    {
        StubPage page = new("/services/test");
        GopherPageResolver resolver = new([page]);

        IGopherPage? result =
            resolver.Resolve("/services/Test");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ReturnsNullForUnknownSelector()
    {
        StubPage page = new("/services/test");
        GopherPageResolver resolver = new([page]);

        IGopherPage? result =
            resolver.Resolve("/services/unknown");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_SupportsNoRegisteredPages()
    {
        GopherPageResolver resolver =
            new(Array.Empty<IGopherPage>());

        IGopherPage? result =
            resolver.Resolve("/services/test");

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_ThrowsForDuplicateNormalizedSelectors()
    {
        StubPage first = new("/services/test");
        StubPage second = new("services/test");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => new GopherPageResolver(
                    [first, second]));

        Assert.Contains(
            "/services/test",
            exception.Message);
    }


    [Fact]
    public void Resolve_SubselectorPage_HandlesChildSelector()
    {
        StubSubselectorPage page = new("/randomsteam");
        GopherPageResolver resolver = new([page]);

        Assert.Same(page, resolver.Resolve("/randomsteam"));
        Assert.Same(page, resolver.Resolve("/randomsteam/76561198000000000"));
    }

    [Fact]
    public void Resolve_SubselectorPage_RespectsPathBoundary()
    {
        StubSubselectorPage page = new("/randomsteam");
        GopherPageResolver resolver = new([page]);

        Assert.Null(resolver.Resolve("/randomsteam-other"));
        Assert.Null(resolver.Resolve("/randomsteaming"));
    }

    [Fact]
    public void Resolve_OrdinaryPage_DoesNotHandleSubselectors()
    {
        StubPage page = new("/qotd");
        GopherPageResolver resolver = new([page]);

        Assert.Same(page, resolver.Resolve("/qotd"));
        Assert.Null(resolver.Resolve("/qotd/anything"));
    }

    [Fact]
    public void Resolve_ExactMatch_TakesPriorityOverSubselector()
    {
        StubSubselectorPage parent = new("/randomsteam");
        StubPage child = new("/randomsteam/about");

        GopherPageResolver resolver = new([parent, child]);

        Assert.Same(child, resolver.Resolve("/randomsteam/about"));
        Assert.Same(parent, resolver.Resolve("/randomsteam/76561198000000000"));
    }

    [Fact]
    public void Resolve_LongestMatchingSubselectorWins()
    {
        StubSubselectorPage parent = new("/games");
        StubSubselectorPage nested = new("/games/steam");

        GopherPageResolver resolver = new([parent, nested]);

        Assert.Same(nested, resolver.Resolve("/games/steam/random"));
        Assert.Same(parent, resolver.Resolve("/games/other"));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/randomsteam/")]
    public void Constructor_RejectsInvalidSubselectorPrefix(string selector)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new GopherPageResolver([new StubSubselectorPage(selector)]));
    }


    private sealed class StubPage(
        string selector) : IGopherPage
    {
        public string Selector { get; } = selector;

        public Task<GopherResponseKind> WriteAsync(
            GopherRequest request,
            Stream output,
            CancellationToken cancellationToken) =>
            Task.FromResult(GopherResponseKind.Text);
    }

    private sealed class StubSubselectorPage(string selector) : IGopherSubselectorPage
    {
        public string Selector => selector;

        public Task<GopherResponseKind> WriteAsync(
            GopherRequest request,
            Stream output,
            CancellationToken cancellationToken) =>
            Task.FromResult(GopherResponseKind.Text);
    }
}
