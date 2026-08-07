/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;
using HappyGopher.Gopher;
using Microsoft.Extensions.Options;

namespace HappyGopher.Pages.Guestbook;

public sealed class ViewGuestbookPage : IGopherPage
{
    private readonly IGuestbookStore _guestbookStore;
    private readonly GuestbookOptions _guestbookOptions;
    private readonly HappyGopherOptions _gopherOptions;

    public ViewGuestbookPage(
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

    public string Selector => "/guestbook";

    public async Task<GopherResponseKind> WriteAsync(
        GopherRequest request,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);

        await using var writer = new GopherResponseWriter(output);

        await writer.WriteInfoAsync(
            "HappyGopher Guestbook",
            cancellationToken);

        await writer.WriteInfoAsync(
            "=====================",
            cancellationToken);

        await writer.WriteInfoAsync(
            string.Empty,
            cancellationToken);

        var entries = await _guestbookStore.GetEntriesAsync(
            take: _guestbookOptions.MaxEntriesDisplayed,
            cancellationToken);

        if (entries.Count == 0)
        {
            await writer.WriteInfoAsync(
                "Nobody has signed the guestbook yet.",
                cancellationToken);

            await writer.WriteInfoAsync(
                "Be the first suspicious internet creature to leave a message.",
                cancellationToken);
        }
        else
        {
            foreach (var entry in entries.Reverse())
            {
                var name = string.IsNullOrWhiteSpace(entry.Name)
                    ? "Anonymous"
                    : entry.Name;

                await writer.WriteInfoAsync(
                    name,
                    cancellationToken);

                await writer.WriteInfoAsync(
                    $"  {entry.Message}",
                    cancellationToken);

                await writer.WriteInfoAsync(
                    string.Empty,
                    cancellationToken);
            }
        }

        if (_guestbookOptions.Enabled)
        {
            await writer.WriteInfoAsync(
                "If you would like to use a nick name, please use the following format: Name | Message",
                cancellationToken);

            await writer.WriteMenuItemAsync(
                type: '7',
                display: "Sign the guestbook",
                selector: "/guestbook/sign",
                host: _gopherOptions.PublicHost,
                port: _gopherOptions.Port,
                cancellationToken);
        }

        await writer.WriteMenuItemAsync(
            type: '1',
            display: "Go back to main menu",
            selector: "",
            host: _gopherOptions.PublicHost,
            port: _gopherOptions.Port,
            cancellationToken);

        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Menu;
    }
}