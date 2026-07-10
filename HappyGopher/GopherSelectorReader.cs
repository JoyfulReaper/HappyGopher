/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Buffers;
using System.Text;

namespace HappyGopher;

internal static class GopherSelectorReader
{
    private static readonly Encoding SelectorEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    public static async Task<string?> ReadAsync(
        Stream stream,
        int maxSelectorBytes,
        int requestTimeoutSeconds,
        CancellationToken stoppingToken)
    {
        // Two extra bytes allow an exact-length selector followed by CRLF.
        int capacity = checked(maxSelectorBytes + 2);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(requestTimeoutSeconds));

        try
        {
            int count = 0;

            while (true)
            {
                int remainingCapacity = capacity - count;

                if (remainingCapacity == 0)
                {
                    throw CreateTooLongException(maxSelectorBytes);
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

                    if (count > maxSelectorBytes)
                    {
                        throw CreateTooLongException(maxSelectorBytes);
                    }

                    return DecodeSelector(buffer, count);
                }

                ReadOnlySpan<byte> received =
                    buffer.AsSpan(count, bytesRead);

                int newlineOffset = received.IndexOf((byte)'\n');

                if (newlineOffset >= 0)
                {
                    int lineLength = count + newlineOffset;

                    if (lineLength > 0 &&
                        buffer[lineLength - 1] == (byte)'\r')
                    {
                        lineLength--;
                    }

                    if (lineLength > maxSelectorBytes)
                    {
                        throw CreateTooLongException(maxSelectorBytes);
                    }

                    return DecodeSelector(buffer, lineLength);
                }

                count += bytesRead;

                if (count > maxSelectorBytes)
                {
                    bool awaitingLfAfterCr =
                        count == maxSelectorBytes + 1 &&
                        buffer[count - 1] == (byte)'\r';

                    if (!awaitingLfAfterCr)
                    {
                        throw CreateTooLongException(maxSelectorBytes);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static InvalidDataException CreateTooLongException(
        int maxSelectorBytes) =>
        new($"Selector exceeded the {maxSelectorBytes} byte limit.");

    private static string DecodeSelector(byte[] buffer, int length) =>
        SelectorEncoding.GetString(buffer, 0, length);
}
