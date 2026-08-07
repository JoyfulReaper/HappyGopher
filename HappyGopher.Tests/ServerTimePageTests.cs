/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;
using HappyGopher.Pages;
using System.Text;

namespace HappyGopher.Tests;

public sealed class ServerTimePageTests
{
    [Fact]
    public async Task WriteAsync_WritesCurrentUtcTimeAsTextResponse()
    {
        DateTimeOffset currentTime = new(
            2026,
            7,
            25,
            20,
            0,
            0,
            TimeSpan.Zero);

        ServerTimePage page =
            new(new FixedTimeProvider(currentTime));

        await using MemoryStream output =
            new();

        GopherResponseKind responseKind =
            await page.WriteAsync(
                new GopherRequest(
                    page.Selector,
                    Input: null),
                output,
                CancellationToken.None);

        Assert.Equal(
            GopherResponseKind.Text,
            responseKind);

        Assert.Equal(
            "/server-time",
            page.Selector);

        Assert.Equal(
            """
            HappyGopher server time
            UTC: 2026-07-25T20:00:00.0000000+00:00
            .

            """.ReplaceLineEndings("\r\n"),
            Encoding.UTF8.GetString(
                output.ToArray()));
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }
}
