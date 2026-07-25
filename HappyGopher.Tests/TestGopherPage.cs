/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;
using HappyGopher.Pages;
using System.Text;

namespace HappyGopher.Tests;

internal sealed class TestGopherPage(
    string selector,
    string response,
    GopherResponseKind responseKind = GopherResponseKind.Text) : IGopherPage
{
    private static readonly Encoding WireEncoding =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string Selector { get; } = selector;

    public async Task<GopherResponseKind> WriteAsync(
        Stream output,
        CancellationToken cancellationToken)
    {
        byte[] bytes = WireEncoding.GetBytes(response);

        await output.WriteAsync(
            bytes,
            cancellationToken);

        await output.FlushAsync(cancellationToken);

        return responseKind;
    }
}