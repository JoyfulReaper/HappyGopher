/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using System.Buffers;
using System.Text;

namespace HappyGopher.Gopher;

internal static class GopherSelectorReader
{
    private static readonly Encoding SelectorEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private const string MalformedRequestMessage =
        "The request contained malformed data.";

    public static async Task<GopherRequest?> ReadAsync(
        Stream stream,
        int maxSelectorBytes,
        int maxInputBytes,
        int requestTimeoutSeconds,
        CancellationToken stoppingToken)
    {
        // A tab plus CRLF may follow exact-length selector and input portions.
        int capacity = checked(maxSelectorBytes + maxInputBytes + 3);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(requestTimeoutSeconds));

        try
        {
            int count = 0;
            while (true)
            {
                int remainingCapacity = capacity - count;
                if (remainingCapacity == 0)
                {
                    throw new InvalidOperationException("The request buffer was exhausted without detecting an exceeded limit.");
                }

                int bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(count, remainingCapacity),
                    timeout.Token);

                if (bytesRead == 0)
                {
                    if (count == 0)
                    {
                        return null;
                    }

                    return ParseRequest(
                        buffer,
                        count,
                        maxSelectorBytes,
                        maxInputBytes);
                }

                count += bytesRead;
                int newlineOffset = buffer.AsSpan(0, count).IndexOf((byte)'\n');

                if (newlineOffset >= 0)
                {
                    int lineLength = newlineOffset;
                    if (lineLength > 0 && buffer[lineLength - 1] == (byte)'\r')
                    {
                        lineLength--;
                    }

                    return ParseRequest(
                        buffer,
                        lineLength,
                        maxSelectorBytes,
                        maxInputBytes);
                }

                ThrowIfPartialRequestExceedsLimits(
                    buffer.AsSpan(0, count),
                    maxSelectorBytes,
                    maxInputBytes);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ThrowIfPartialRequestExceedsLimits(
        ReadOnlySpan<byte> request,
        int maxSelectorBytes,
        int maxInputBytes)
    {
        int tabIndex = request.IndexOf((byte)'\t');
        if (tabIndex < 0)
        {
            ValidatePartialPortion(
                request,
                maxSelectorBytes,
                CreateSelectorTooLongException);

            return;
        }

        if (tabIndex > maxSelectorBytes)
        {
            throw CreateSelectorTooLongException(maxSelectorBytes);
        }

        ValidatePartialPortion(
            request[(tabIndex + 1)..],
            maxInputBytes,
            CreateInputTooLongException);
    }

    private static void ValidatePartialPortion(
        ReadOnlySpan<byte> portion,
        int maxBytes,
        Func<int, InvalidDataException> createException)
    {
        if (portion.Length <= maxBytes)
        {
            return;
        }

        bool awaitingLfAfterCr =
            portion.Length == maxBytes + 1 &&
            portion[^1] == (byte)'\r';

        if (!awaitingLfAfterCr)
        {
            throw createException(maxBytes);
        }
    }

    private static GopherRequest ParseRequest(
        byte[] buffer,
        int lineLength,
        int maxSelectorBytes,
        int maxInputBytes)
    {
        ReadOnlySpan<byte> request = buffer.AsSpan(0, lineLength);

        if (request.Contains((byte)'\0') || request.Contains((byte)'\r'))
        {
            throw new InvalidDataException(MalformedRequestMessage);
        }

        int tabIndex = request.IndexOf((byte)'\t');

        int selectorLength =
            tabIndex >= 0
                ? tabIndex
                : lineLength;

        if (selectorLength > maxSelectorBytes)
        {
            throw CreateSelectorTooLongException(maxSelectorBytes);
        }

        string selector = DecodeUtf8(request[..selectorLength]);

        if (tabIndex < 0)
        {
            return new GopherRequest(selector, Input: null);
        }

        int inputLength = lineLength - tabIndex - 1;
        if (inputLength > maxInputBytes)
        {
            throw CreateInputTooLongException(maxInputBytes);
        }

        string input = DecodeUtf8(request[(tabIndex + 1)..]);

        return new GopherRequest(selector, input);
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> value)
    {
        try
        {
            return SelectorEncoding.GetString(value);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                MalformedRequestMessage,
                exception);
        }
    }

    private static InvalidDataException CreateSelectorTooLongException(
        int maxSelectorBytes) =>
        new($"Selector exceeded the {maxSelectorBytes} byte limit.");

    private static InvalidDataException CreateInputTooLongException(
        int maxInputBytes) =>
        new($"Input exceeded the {maxInputBytes} byte limit.");
}
