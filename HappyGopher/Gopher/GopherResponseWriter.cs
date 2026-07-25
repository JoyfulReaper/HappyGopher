/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Globalization;
using System.Text;

namespace HappyGopher.Gopher;

/// <summary>
/// Writes correctly formatted text and menu responses to a Gopher connection.
/// </summary>
public sealed class GopherResponseWriter : IAsyncDisposable
{
    private static readonly Encoding WireEncoding =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly StreamWriter _writer;
    private bool _completed;

    public GopherResponseWriter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _writer = new StreamWriter(
            output,
            WireEncoding,
            bufferSize: 4096,
            leaveOpen: true)
        {
            NewLine = "\r\n"
        };
    }

    /// <summary>
    /// Writes one line of a Gopher text response.
    /// </summary>
    public async Task WriteTextLineAsync(
        string line,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        ThrowIfCompleted();

        if (line.StartsWith('.'))
        {
            line = "." + line;
        }

        await _writer.WriteLineAsync(
            line.AsMemory(),
            cancellationToken);
    }

    /// <summary>
    /// Writes one Gopher menu item.
    /// </summary>
    public async Task WriteMenuItemAsync(
        char type,
        string display,
        string selector,
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(host);

        ThrowIfCompleted();

        string line = string.Concat(type,
            GopherMenuFields.Sanitize(display),
            "\t",
            GopherMenuFields.Sanitize(selector),
            "\t",
            GopherMenuFields.Sanitize(host),
            "\t",
            port.ToString(CultureInfo.InvariantCulture));

        await _writer.WriteLineAsync(
            line.AsMemory(),
            cancellationToken);
    }

    /// <summary>
    /// Writes a non-selectable informational menu item.
    /// </summary>
    public Task WriteInfoAsync(
        string display,
        CancellationToken cancellationToken = default) =>
        WriteMenuItemAsync(
            'i',
            display,
            "fake",
            "(NULL)",
            0,
            cancellationToken);

    /// <summary>
    /// Writes a Gopher error menu item.
    /// </summary>
    public Task WriteErrorAsync(
        string message,
        string host,
        int port,
        CancellationToken cancellationToken = default) =>
        WriteMenuItemAsync(
            '3',
            message,
            "error",
            host,
            port,
            cancellationToken);

    /// <summary>
    /// Writes the terminating dot and flushes the response.
    /// </summary>
    public async Task CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;

        await _writer.WriteLineAsync(".".AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() =>
        _writer.DisposeAsync();

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException(
                "The Gopher response has already been completed.");
        }
    }
}