/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Text;

namespace HappyGopher.Tests;

public sealed class GopherContentStoreTests
{
    [Fact]
    public async Task WriteResponseAsync_EmptySelectorResolvesToContentRoot()
    {
        using TestContentStore test = new();
        test.WriteText("gophermap", "iRoot menu");

        string response = await test.GetResponseAsync(string.Empty);

        Assert.Contains("iRoot menu\tfake\t(NULL)\t0\r\n", response);
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
