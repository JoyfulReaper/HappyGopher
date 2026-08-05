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
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5); // TODO: Make configurable

    private string? _lastSubmissionKey;
    private DateTimeOffset _lastSubmissionAt;

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

    public async Task<bool> AddEntryAsync(
        GuestbookEntry entry,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisabled();
        ArgumentNullException.ThrowIfNull(entry);

        string submissionKey = CreateSubmissionKey(entry);

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // NOTE: Some Gopher clients, including Gophie, may replay type-7 search requests and create duplicate guestbook entries.
            if (string.Equals(submissionKey, _lastSubmissionKey, StringComparison.Ordinal) &&
                now - _lastSubmissionAt <= DuplicateWindow)
            {
                _logger.LogInformation("Suppressed replayed guestbook submission.");
                return false;
            }

            string? directory = Path.GetDirectoryName(_fullPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(entry);

            await using var fs = File.Open(_fullPath, _fsAppendOptions);
            await using var writer = new StreamWriter(fs);

            await writer.WriteLineAsync(
                json.AsMemory(),
                cancellationToken);

            await writer.FlushAsync(cancellationToken);

            _lastSubmissionKey = submissionKey;
            _lastSubmissionAt = now;

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string CreateSubmissionKey(GuestbookEntry entry)
    {
        string name = entry.Name?.Trim() ?? string.Empty;
        string message = entry.Message.Trim();

        return $"{name.ToUpperInvariant()}\n{message}";
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
