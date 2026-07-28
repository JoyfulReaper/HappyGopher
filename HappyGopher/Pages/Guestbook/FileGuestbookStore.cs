/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 * 
 * Let's not bring a database into this...
 * 
 */

using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HappyGopher.Pages.Guestbook;

public sealed class FileGuestbookStore : IGuestbookStore
{
    private readonly GuestbookOptions _guestbookOptions;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly string _fullPath;
    private readonly ILogger<FileGuestbookStore> _logger;

    private readonly FileStreamOptions _fsAppendOptions = new FileStreamOptions
    {
        Mode = FileMode.Append,
        Access = FileAccess.Write,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous
    };

    private readonly FileStreamOptions _fsReadOptions = new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous
    };

    public FileGuestbookStore(IOptions<GuestbookOptions> options, ILogger<FileGuestbookStore> logger)
    {
        _guestbookOptions = options.Value;
        _fullPath = Path.GetFullPath(_guestbookOptions.DataPath);
        _logger = logger;
    }

    public async Task AddEntryAsync(GuestbookEntry entry, CancellationToken cancellationToken = default)
    {
        ThrowIfDisabled();
        ArgumentNullException.ThrowIfNull(entry);

        string? directory = Path.GetDirectoryName(_fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(entry);
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            await using var fs = File.Open(_fullPath, _fsAppendOptions);
            using var writer = new StreamWriter(fs);

            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);

            // TODO Create an integration event
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<GuestbookEntry>> GetEntriesAsync(int take, CancellationToken cancellationToken = default)
    {
        // NOTE: This scans the entire file to retain the newest entries.
        // That is acceptable for a small Gopher guestbook. If the file grows
        // significantly, consider reading records backward from the end.
        ThrowIfDisabled();

        if (take < 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        if (take == 0)
            return Array.Empty<GuestbookEntry>();

        await _semaphore.WaitAsync(cancellationToken);
        var queue = new Queue<GuestbookEntry>();

        try
        {
            if (!File.Exists(_fullPath))
            {
                return Array.Empty<GuestbookEntry>();
            }

            await using var fs = File.Open(_fullPath, _fsReadOptions);
            using var reader = new StreamReader(fs);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<GuestbookEntry>(line);
                    if (entry is not null)
                    {
                        queue.Enqueue(entry);

                        if (queue.Count > take)
                        {
                            queue.Dequeue();
                        }
                    }
                }
                catch (JsonException ex)
                {
                    // TODO: Create an integration event
                    _logger.LogError(ex, "Failed to deserialize guestbook entry");
                    continue;
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return queue.ToList();
    }

    private void ThrowIfDisabled()
    {
        if (!_guestbookOptions.Enabled)
        {
            throw new InvalidOperationException("The Guestbook is not enabled");
        }
    }
}
