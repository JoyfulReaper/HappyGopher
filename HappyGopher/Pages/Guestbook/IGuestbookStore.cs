/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Pages.Guestbook;

public interface IGuestbookStore
{
    Task<IReadOnlyList<GuestbookEntry>> GetEntriesAsync(int take, CancellationToken cancellationToken = default);
    Task AddEntryAsync(GuestbookEntry entry, CancellationToken cancellationToken = default);
}
