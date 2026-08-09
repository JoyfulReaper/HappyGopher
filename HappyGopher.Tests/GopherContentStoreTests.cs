/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Pages;

namespace HappyGopher.Tests;

public sealed class GopherContentStoreTests
{
    [Fact]
    public async Task TextFile_UsesCrlfDotStuffingAndTerminator()
    {
        using TestContentStore content = new();

        content.WriteText(
            "about.txt",
            "Hello\n.hidden");

        string response =
            await content.GetResponseAsync("/about.txt");

        Assert.Equal(
            "Hello\r\n..hidden\r\n.\r\n",
            response);
    }

    [Fact]
    public async Task WriteResponseAsync_EmptySelectorResolvesToContentRoot()
    {
        using TestContentStore test = new();
        test.WriteText("gophermap", "iRoot menu");

        string response = await test.GetResponseAsync(string.Empty);

        Assert.Contains("iRoot menu\tfake\t(NULL)\t0\r\n", response);
    }

    [Fact]
    public async Task WriteResponseAsync_SlashSelectorResolvesToContentRoot()
    {
        using TestContentStore test = new();
        test.WriteText("gophermap", "iRoot menu");

        string response = await test.GetResponseAsync("/");

        Assert.Contains("iRoot menu\tfake\t(NULL)\t0\r\n", response);
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("/file.txt")]
    public async Task WriteResponseAsync_AcceptsOptionalSingleLeadingSlash(
        string selector)
    {
        using TestContentStore test = new();
        test.WriteText("file.txt", "Static file");

        string response = await test.GetResponseAsync(selector);

        Assert.Equal("Static file\r\n.\r\n", response);
    }

    [Theory]
    [InlineData("//file.txt")]
    [InlineData("\\file.txt")]
    [InlineData("/foo\\bar")]
    [InlineData("/./file.txt")]
    [InlineData("/foo/./bar")]
    [InlineData("/x/../file.txt")]
    [InlineData("../file.txt")]
    [InlineData("foo/../file.txt")]
    [InlineData("/foo//bar")]
    [InlineData("/foo/bar/")]
    public async Task WriteResponseAsync_RejectsNoncanonicalStaticSelector(
        string selector)
    {
        using TestContentStore test = new();
        test.WriteText("file.txt", "Static file");

        string response = await test.GetResponseAsync(selector);

        AssertSingleError(response, "Invalid Selector.");
        Assert.DoesNotContain("Static file", response);
    }

    [Theory]
    [InlineData("/file..txt")]
    [InlineData("/foo.bar")]
    [InlineData("/.well-known")]
    public async Task WriteResponseAsync_AcceptsOrdinaryDotsInFileName(
        string selector)
    {
        using TestContentStore test = new();
        test.WriteText(selector[1..], "Dotted file");

        string response = await test.GetResponseAsync(selector);

        Assert.Contains("Dotted file", response);
        Assert.DoesNotContain("3Invalid Selector.", response);
        Assert.DoesNotContain("3Selector not found.", response);
    }

    [Fact]
    public async Task WriteResponseAsync_DynamicAliasMissCannotReachStaticFile()
    {
        using TestContentStore test = new();
        test.WriteText("healthz", "Static health response");
        TestGopherPage page = new(
            selector: "/healthz",
            response: "Dynamic health response\r\n.\r\n");
        GopherPageResolver resolver = new([page]);

        Assert.Null(resolver.Resolve("//healthz"));

        string response = await test.GetResponseAsync("//healthz");

        AssertSingleError(response, "Invalid Selector.");
        Assert.DoesNotContain("Static health response", response);
    }

    [Fact]
    public async Task WriteResponseAsync_ReturnsNormalTextFile()
    {
        using TestContentStore test = new();
        test.WriteText("about.txt", "Hello");

        string response = await test.GetResponseAsync("/about.txt");

        Assert.Equal("Hello\r\n.\r\n", response);
    }

    [Fact]
    public async Task WriteResponseAsync_TextResponsesUseCrLf()
    {
        using TestContentStore test = new();
        test.WriteText("about.txt", "one\ntwo");

        string response = await test.GetResponseAsync("/about.txt");

        Assert.Equal("one\r\ntwo\r\n.\r\n", response);
        Assert.DoesNotContain("one\ntwo", response);
    }

    [Fact]
    public async Task WriteResponseAsync_TextResponsesEndWithGopherTerminator()
    {
        using TestContentStore test = new();
        test.WriteText("about.txt", "Hello");

        string response = await test.GetResponseAsync("/about.txt");

        Assert.EndsWith(".\r\n", response);
    }

    [Fact]
    public async Task WriteResponseAsync_DotStuffsTextLinesBeginningWithDot()
    {
        using TestContentStore test = new();
        test.WriteText("about.txt", ".hidden\n..twice");

        string response = await test.GetResponseAsync("/about.txt");

        Assert.Equal("..hidden\r\n...twice\r\n.\r\n", response);
    }

    [Fact]
    public async Task WriteResponseAsync_CopiesBinaryFilesWithoutTextTermination()
    {
        using TestContentStore test = new();
        byte[] contents = [0, 1, 2, 3, 46, 13, 10];
        test.WriteBytes("data.bin", contents);

        await using MemoryStream output = new();
        await test.CreateStore().WriteResponseAsync("/data.bin", output, CancellationToken.None);

        Assert.Equal(contents, output.ToArray());
    }

    [Fact]
    public async Task WriteResponseAsync_MissingSelectorReturnsExactlyOneTypeThreeError()
    {
        using TestContentStore test = new();

        string response = await test.GetResponseAsync("/missing.txt");

        AssertSingleError(response, "Selector not found.");
    }

    [Theory]
    [InlineData("/../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("/C:/secret.txt")]
    public async Task WriteResponseAsync_InvalidSelectorsReturnExactlyOneTypeThreeError(
        string selector)
    {
        using TestContentStore test = new();

        string response = await test.GetResponseAsync(selector);

        AssertSingleError(response, "Invalid Selector.");
    }

    [Fact]
    public async Task WriteResponseAsync_GophermapCannotBeFetchedDirectly()
    {
        using TestContentStore test = new();
        test.WriteText("gophermap", "iRoot");

        string response = await test.GetResponseAsync("/gophermap");

        AssertSingleError(response, "Selector not found.");
    }

    [Fact]
    public async Task WriteResponseAsync_DirectoryWithGophermapRendersMap()
    {
        using TestContentStore test = new();
        test.WriteText("downloads/gophermap", "iDownloads");
        test.WriteText("downloads/readme.txt", "Read me");

        string response = await test.GetResponseAsync("/downloads");

        Assert.Equal("iDownloads\tfake\t(NULL)\t0\r\n.\r\n", response);
    }

    [Fact]
    public async Task WriteResponseAsync_DirectoryWithoutGophermapGeneratesSortedMenu()
    {
        using TestContentStore test = new();
        Directory.CreateDirectory(Path.Combine(test.Root, "zeta"));
        Directory.CreateDirectory(Path.Combine(test.Root, "alpha"));
        test.WriteText("z.txt", "z");
        test.WriteText("a.txt", "a");

        string response = await test.GetResponseAsync(string.Empty);

        string[] lines = DataLines(response);
        Assert.Equal(
            [
                "1alpha/\t/alpha\tgopher.test\t7070",
                "1zeta/\t/zeta\tgopher.test\t7070",
                "0a.txt\t/a.txt\tgopher.test\t7070",
                "0z.txt\t/z.txt\tgopher.test\t7070"
            ],
            lines);
    }

    [Fact]
    public async Task WriteResponseAsync_GeneratedFileEntriesUseCorrectItemTypes()
    {
        using TestContentStore test = new();
        Directory.CreateDirectory(Path.Combine(test.Root, "docs"));
        test.WriteText("about.txt", "about");
        test.WriteBytes("image.gif", [1]);
        test.WriteBytes("image.png", [2]);
        test.WriteBytes("archive.bin", [3]);

        string response = await test.GetResponseAsync(string.Empty);

        Assert.Contains("1docs/\t/docs\tgopher.test\t7070", response);
        Assert.Contains("0about.txt\t/about.txt\tgopher.test\t7070", response);
        Assert.Contains("gimage.gif\t/image.gif\tgopher.test\t7070", response);
        Assert.Contains("Iimage.png\t/image.png\tgopher.test\t7070", response);
        Assert.Contains("9archive.bin\t/archive.bin\tgopher.test\t7070", response);
    }

    [Fact]
    public async Task WriteResponseAsync_GeneratedDirectorySelectorsRemainUsable()
    {
        using TestContentStore test = new();
        test.WriteText("docs/readme.txt", "Generated selector target");

        string rootResponse = await test.GetResponseAsync(string.Empty);
        string directoryResponse = await test.GetResponseAsync("/docs");
        string fileResponse = await test.GetResponseAsync("/docs/readme.txt");

        Assert.Contains("1docs/\t/docs\tgopher.test\t7070\r\n", rootResponse);
        Assert.Contains(
            "0readme.txt\t/docs/readme.txt\tgopher.test\t7070\r\n",
            directoryResponse);
        Assert.Equal("Generated selector target\r\n.\r\n", fileResponse);
    }

    [Fact]
    public async Task WriteResponseAsync_RelativeGophermapSelectorsResolveFromMapDirectory()
    {
        using TestContentStore test = new();
        test.WriteText("docs/gophermap", "0Read me\treadme.txt");
        test.WriteText("docs/readme.txt", "Read me");

        string response = await test.GetResponseAsync("/docs");

        Assert.Contains("0Read me\t/docs/readme.txt\tgopher.test\t7070", response);
    }

    [Theory]
    [InlineData(1234, 1234)]
    [InlineData(0, 7070)]
    [InlineData(65536, 7070)]
    public async Task WriteResponseAsync_GophermapPortsAreValidated(
        int configuredPort,
        int expectedPort)
    {
        using TestContentStore test = new();
        test.WriteText("gophermap", $"0Remote\t/remote\tremote.test\t{configuredPort}");

        string response = await test.GetResponseAsync(string.Empty);

        Assert.Contains($"0Remote\t/remote\tremote.test\t{expectedPort}\r\n", response);
    }

    [Fact]
    public async Task WriteResponseAsync_MenuFieldsSanitizeTabs()
    {
        using TestContentStore test = new();
        test.WriteText("gophermap", "xThis\tis\tinfo");

        string response = await test.GetResponseAsync(string.Empty);

        Assert.Contains("ixThis is info\tfake\t(NULL)\t0\r\n", response);
    }

    private static string[] DataLines(string response) =>
        response.Split(
                "\r\n",
                StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line != ".")
            .ToArray();

    private static void AssertSingleError(string response, string message)
    {
        string[] lines = response.Split(
            "\r\n",
            StringSplitOptions.RemoveEmptyEntries);

        string errorLine = Assert.Single(lines, line => line.StartsWith('3'));
        Assert.StartsWith($"3{message}\terror\tgopher.test\t7070", errorLine);
        Assert.Equal(".", lines[^1]);
    }
}
