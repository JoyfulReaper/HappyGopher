/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;
using HappyGopher.Pages;
using System.Text;

namespace HappyGopher.Tests;

public sealed class HealthPageTests
{
    [Fact]
    public async Task WriteAsync_WritesTerminatedOkTextResponse()
    {
        HealthPage page = new();
        await using MemoryStream output = new();

        GopherResponseKind responseKind = await page.WriteAsync(
            new GopherRequest(page.Selector, Input: null),
            output,
            CancellationToken.None);

        Assert.Equal("/healthz", page.Selector);
        Assert.Equal(GopherResponseKind.Text, responseKind);
        Assert.Equal(
            "OK\r\n.\r\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task WriteAsync_RespectsCancellation()
    {
        HealthPage page = new();
        await using MemoryStream output = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            page.WriteAsync(
                new GopherRequest(page.Selector, Input: null),
                output,
                cancellation.Token));
    }
}
