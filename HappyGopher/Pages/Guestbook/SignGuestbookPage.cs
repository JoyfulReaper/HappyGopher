/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

// TODO: MissionControl Telemetry integration for guestbook signings

using HappyGopher.Gopher;
using Microsoft.Extensions.Options;

namespace HappyGopher.Pages.Guestbook;

public sealed class SignGuestbookPage : IGopherPage
{
    private const int MaxNameLength = 40; // TODO: Make configurable
    private const int MaxMessageLength = 500; // TODO: Make configurable

    private readonly IGuestbookStore _guestbookStore;
    private readonly GuestbookOptions _guestbookOptions;
    private readonly HappyGopherOptions _gopherOptions;

    public SignGuestbookPage(
        IGuestbookStore guestbookStore,
        IOptions<GuestbookOptions> guestbookOptions,
        IOptions<HappyGopherOptions> gopherOptions)
    {
        ArgumentNullException.ThrowIfNull(guestbookStore);
        ArgumentNullException.ThrowIfNull(guestbookOptions);
        ArgumentNullException.ThrowIfNull(gopherOptions);

        _guestbookStore = guestbookStore;
        _guestbookOptions = guestbookOptions.Value;
        _gopherOptions = gopherOptions.Value;
    }

    public string Selector => "guestbook/sign";

    public async Task<GopherResponseKind> WriteAsync(
        GopherRequest request,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);

        await using var writer = new GopherResponseWriter(output);

        if (!_guestbookOptions.Enabled)
        {
            await writer.WriteErrorAsync(
                "The guestbook is currently disabled.",
                _gopherOptions.PublicHost,
                _gopherOptions.Port,
                cancellationToken);

            await writer.CompleteAsync(cancellationToken);

            return GopherResponseKind.InvalidSelector;
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            await WriteInstructionsAsync(writer, cancellationToken);
            await writer.CompleteAsync(cancellationToken);

            return GopherResponseKind.Menu;
        }

        var parseResult = ParseInput(request.Input);

        if (!parseResult.Succeeded)
        {
            await writer.WriteErrorAsync(
                parseResult.Error!,
                _gopherOptions.PublicHost,
                _gopherOptions.Port,
                cancellationToken);

            await writer.WriteInfoAsync(
                string.Empty,
                cancellationToken);

            await WriteInstructionsAsync(writer, cancellationToken);
            await writer.CompleteAsync(cancellationToken);

            return GopherResponseKind.Menu;
        }

        var entry = new GuestbookEntry
        {
            Name = parseResult.Name,
            Message = parseResult.Message!
        };

        await _guestbookStore.AddEntryAsync(entry, cancellationToken);

        var displayName = string.IsNullOrWhiteSpace(entry.Name)
            ? "Anonymous"
            : entry.Name;

        await writer.WriteInfoAsync(
            "Guestbook signed successfully.",
            cancellationToken);

        await writer.WriteInfoAsync(
            string.Empty,
            cancellationToken);

        await writer.WriteInfoAsync(
            $"Thanks, {displayName}.",
            cancellationToken);

        await writer.WriteInfoAsync(
            string.Empty,
            cancellationToken);

        await writer.WriteMenuItemAsync(
            type: '1',
            display: "View the guestbook",
            selector: "guestbook",
            host: _gopherOptions.PublicHost,
            port: _gopherOptions.Port,
            cancellationToken);

        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Menu;
    }

    private async Task WriteInstructionsAsync(
        GopherResponseWriter writer,
        CancellationToken cancellationToken)
    {
        await writer.WriteInfoAsync(
            "Sign the HappyGopher Guestbook",
            cancellationToken);

        await writer.WriteInfoAsync(
            "==============================",
            cancellationToken);

        await writer.WriteInfoAsync(
            string.Empty,
            cancellationToken);

        await writer.WriteInfoAsync(
            "Enter your message using one of these formats:",
            cancellationToken);

        await writer.WriteInfoAsync(
            "Name | Message",
            cancellationToken);

        await writer.WriteInfoAsync(
            "Message",
            cancellationToken);

        await writer.WriteInfoAsync(
            string.Empty,
            cancellationToken);

        await writer.WriteInfoAsync(
            $"Maximum name length: {MaxNameLength}",
            cancellationToken);

        await writer.WriteInfoAsync(
            $"Maximum message length: {MaxMessageLength}",
            cancellationToken);

        await writer.WriteInfoAsync(
            string.Empty,
            cancellationToken);

        await writer.WriteMenuItemAsync(
            type: '1',
            display: "Return to the guestbook",
            selector: "guestbook",
            host: _gopherOptions.PublicHost,
            port: _gopherOptions.Port,
            cancellationToken);
    }

    private static GuestbookParseResult ParseInput(string input)
    {
        var sanitized = Sanitize(input);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return GuestbookParseResult.Failure(
                "A message is required.");
        }

        string? name = null;
        string message;

        var separatorIndex = sanitized.IndexOf('|');

        if (separatorIndex >= 0)
        {
            name = sanitized[..separatorIndex].Trim();
            message = sanitized[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                name = null;
            }
        }
        else
        {
            message = sanitized.Trim();
        }

        if (name is { Length: > MaxNameLength })
        {
            return GuestbookParseResult.Failure(
                $"Name cannot exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return GuestbookParseResult.Failure(
                "A message is required.");
        }

        if (message.Length > MaxMessageLength)
        {
            return GuestbookParseResult.Failure(
                $"Message cannot exceed {MaxMessageLength} characters.");
        }

        return GuestbookParseResult.Success(name, message);
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace('\0', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();
    }

    private sealed record GuestbookParseResult(
        bool Succeeded,
        string? Name,
        string? Message,
        string? Error)
    {
        public static GuestbookParseResult Success(
            string? name,
            string message) =>
            new(true, name, message, null);

        public static GuestbookParseResult Failure(string error) =>
            new(false, null, null, error);
    }
}