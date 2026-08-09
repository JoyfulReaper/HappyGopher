/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using System.Text;

namespace HappyGopher.Tests;

public sealed class GopherResponseWriterTests
{
    [Fact]
    public async Task WriteTextLineAsync_OrdinaryLineUsesCrlf()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteTextLineAsync("Hello");
        }

        Assert.Equal(
            "Hello\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteTextLineAsync_EmptyLineIsValid()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteTextLineAsync(string.Empty);
        }

        Assert.Equal(
            "\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData(".hello", "..hello\r\n")]
    [InlineData(".", "..\r\n")]
    public async Task WriteTextLineAsync_DotStuffsLine(
        string line,
        string expected)
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteTextLineAsync(line);
        }

        Assert.Equal(
            expected,
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData("hello\rhidden")]
    [InlineData("hello\nhidden")]
    [InlineData("hello\r\nhidden")]
    public async Task WriteTextLineAsync_RejectsEmbeddedLineEndings(
        string line)
    {
        await using MemoryStream output = new();
        await using GopherResponseWriter writer = new(output);

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => writer.WriteTextLineAsync(line));

        Assert.Equal("line", exception.ParamName);
    }

    [Fact]
    public async Task WriteTextLineAsync_RejectedLineDoesNotPreventValidWrite()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => writer.WriteTextLineAsync("hello\r\n.\r\nhidden"));

            await writer.WriteTextLineAsync("Hello");
            await writer.CompleteAsync();
        }

        Assert.Equal(
            "Hello\r\n.\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteMenuItemAsync_ValidItemOutputIsUnchanged()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteMenuItemAsync(
                type: '1',
                display: "Documentation",
                selector: "/docs",
                host: "gopher.test",
                port: 7070);
        }

        Assert.Equal(
            "1Documentation\t/docs\tgopher.test\t7070\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteInfoAsync_PreservesNullHostAndZeroPortConvention()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteInfoAsync("Information");
        }

        Assert.Equal(
            "iInformation\tfake\t(NULL)\t0\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData("Dis\0play", "/docs", "gopher.test", "1Dis play\t/docs\tgopher.test\t7070\r\n")]
    [InlineData("Display", "/do\0cs", "gopher.test", "1Display\t/do cs\tgopher.test\t7070\r\n")]
    [InlineData("Display", "/docs", "gopher\0.test", "1Display\t/docs\tgopher .test\t7070\r\n")]
    public async Task WriteMenuItemAsync_SanitizesNulInFields(
        string display,
        string selector,
        string host,
        string expected)
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteMenuItemAsync(
                type: '1',
                display,
                selector,
                host,
                port: 7070);
        }

        Assert.Equal(
            expected,
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteMenuItemAsync_SanitizesFields()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteMenuItemAsync(
                type: '1',
                display: "Docs\tHere",
                selector: "/docs\rchild",
                host: "gopher.test\ninvalid",
                port: 7070);

            await writer.CompleteAsync();
        }

        Assert.Equal(
            "1Docs Here\t/docs child\tgopher.test invalid\t7070\r\n.\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData('\0')]
    [InlineData('\u001F')]
    [InlineData('.')]
    [InlineData(' ')]
    [InlineData('\t')]
    [InlineData('é')]
    public async Task WriteMenuItemAsync_RejectsInvalidItemType(
        char type)
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            ArgumentOutOfRangeException exception =
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => writer.WriteMenuItemAsync(
                        type,
                        "Display",
                        "/selector",
                        "gopher.test",
                        7070));

            Assert.Equal("type", exception.ParamName);

            await writer.WriteInfoAsync("Still valid");
        }

        Assert.Equal(
            "iStill valid\tfake\t(NULL)\t0\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteMenuItemAsync_AcceptsPrintableExtensionItemType()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteMenuItemAsync(
                'x',
                "Extension",
                "/extension",
                "gopher.test",
                7070);
        }

        Assert.Equal(
            "xExtension\t/extension\tgopher.test\t7070\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task WriteMenuItemAsync_RejectsInvalidSelectablePort(
        int port)
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            ArgumentOutOfRangeException exception =
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => writer.WriteMenuItemAsync(
                        '1',
                        "Display",
                        "/selector",
                        "gopher.test",
                        port));

            Assert.Equal("port", exception.ParamName);

            await writer.WriteInfoAsync("Still valid");
        }

        Assert.Equal(
            "iStill valid\tfake\t(NULL)\t0\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteMenuItemAsync_AcceptsMaximumPort()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteMenuItemAsync(
                '1',
                "Display",
                "/selector",
                "gopher.test",
                65535);
        }

        Assert.Equal(
            "1Display\t/selector\tgopher.test\t65535\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task DisposeAsync_LeavesOutputStreamOpen()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteInfoAsync("Information");
            await writer.CompleteAsync();
        }

        Assert.True(output.CanWrite);

        output.WriteByte(0);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotWriteMultipleTerminators()
    {
        await using MemoryStream output = new();

        await using (GopherResponseWriter writer = new(output))
        {
            await writer.WriteInfoAsync("Information");
            await writer.CompleteAsync();
            await writer.CompleteAsync();
        }

        Assert.Equal(
            "iInformation\tfake\t(NULL)\t0\r\n.\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }
}
