/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Pages.Guestbook;

public interface IGuestbookStore
{
    Task<bool> AddEntryAsync(GuestbookEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuestbookEntry>> GetEntriesAsync(int take, CancellationToken cancellationToken = default);
}
