/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;
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
}
