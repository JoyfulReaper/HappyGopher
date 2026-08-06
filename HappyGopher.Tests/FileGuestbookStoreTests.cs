using HappyGopher.Pages.Guestbook;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HappyGopher.Tests;

public sealed class FileGuestbookStoreTests
{
    [Fact]
    public async Task AddEntryAsync_WritesOneValidJsonObjectPerLine()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string dataPath = temporaryDirectory.GetPath("guestbook.jsonl");
        FileGuestbookStore store = CreateStore(dataPath);

        bool added = await store.AddEntryAsync(new GuestbookEntry
        {
            Name = "Kyle",
            Message = "Hello, Gopher"
        });

        string line = Assert.Single(await File.ReadAllLinesAsync(dataPath));
        GuestbookEntry entry = Assert.IsType<GuestbookEntry>(
            JsonSerializer.Deserialize<GuestbookEntry>(line));
        Assert.True(added);
        Assert.Equal("Kyle", entry.Name);
        Assert.Equal("Hello, Gopher", entry.Message);
    }

    [Fact]
    public async Task GetEntriesAsync_ReadsEntriesBack()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileGuestbookStore store = CreateStore(
            temporaryDirectory.GetPath("guestbook.jsonl"));

        await store.AddEntryAsync(new GuestbookEntry
        {
            Name = "Ada",
            Message = "First"
        });
        await store.AddEntryAsync(new GuestbookEntry
        {
            Name = "Grace",
            Message = "Second"
        });

        IReadOnlyList<GuestbookEntry> entries =
            await store.GetEntriesAsync(take: 10);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("Ada", entry.Name);
                Assert.Equal("First", entry.Message);
            },
            entry =>
            {
                Assert.Equal("Grace", entry.Name);
                Assert.Equal("Second", entry.Message);
            });
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsOnlyNewestRequestedEntries()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileGuestbookStore store = CreateStore(
            temporaryDirectory.GetPath("guestbook.jsonl"));

        for (int index = 1; index <= 5; index++)
        {
            await store.AddEntryAsync(new GuestbookEntry
            {
                Message = $"Message {index}"
            });
        }

        IReadOnlyList<GuestbookEntry> entries =
            await store.GetEntriesAsync(take: 2);

        Assert.Equal(
            ["Message 4", "Message 5"],
            entries.Select(entry => entry.Message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetEntriesAsync_ReturnsEmptyForMissingOrEmptyFile(
        bool createEmptyFile)
    {
        using TemporaryDirectory temporaryDirectory = new();
        string dataPath = temporaryDirectory.GetPath("guestbook.jsonl");
        if (createEmptyFile)
        {
            await File.WriteAllTextAsync(dataPath, string.Empty);
        }

        FileGuestbookStore store = CreateStore(dataPath);

        IReadOnlyList<GuestbookEntry> entries =
            await store.GetEntriesAsync(take: 10);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetEntriesAsync_SkipsMalformedLinesAndKeepsValidEntries()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string dataPath = temporaryDirectory.GetPath("guestbook.jsonl");
        await File.WriteAllLinesAsync(dataPath,
        [
            JsonSerializer.Serialize(new GuestbookEntry
            {
                Name = "Valid One",
                Message = "Before"
            }),
            "{this is not JSON}",
            JsonSerializer.Serialize(new GuestbookEntry
            {
                Name = "Valid Two",
                Message = "After"
            })
        ]);
        FileGuestbookStore store = CreateStore(dataPath);

        IReadOnlyList<GuestbookEntry> entries =
            await store.GetEntriesAsync(take: 10);

        Assert.Equal(
            ["Before", "After"],
            entries.Select(entry => entry.Message));
    }

    [Fact]
    public async Task AddEntryAsync_SerializesConcurrentWritesWithoutCorruption()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string dataPath = temporaryDirectory.GetPath("guestbook.jsonl");
        FileGuestbookStore store = CreateStore(dataPath);

        Task<bool>[] writes = Enumerable.Range(0, 50)
            .Select(index => store.AddEntryAsync(new GuestbookEntry
            {
                Name = $"Guest {index}",
                Message = $"Concurrent message {index}"
            }))
            .ToArray();

        bool[] results = await Task.WhenAll(writes);
        string[] lines = await File.ReadAllLinesAsync(dataPath);
        GuestbookEntry[] entries = lines
            .Select(line => JsonSerializer.Deserialize<GuestbookEntry>(line))
            .OfType<GuestbookEntry>()
            .ToArray();

        Assert.All(results, Assert.True);
        Assert.Equal(writes.Length, lines.Length);
        Assert.Equal(writes.Length, entries.Length);
        Assert.Equal(
            writes.Length,
            entries.Select(entry => entry.Message).Distinct().Count());
    }

    [Fact]
    public async Task AddEntryAsync_SuppressesIdenticalSubmissionInsideDuplicateWindow()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ManualTimeProvider timeProvider = new(
            new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
        FileGuestbookStore store = CreateStore(
            temporaryDirectory.GetPath("guestbook.jsonl"),
            timeProvider);
        GuestbookEntry entry = new()
        {
            Name = "Kyle",
            Message = "Replay me"
        };

        bool firstAdded = await store.AddEntryAsync(entry);
        bool replayAdded = await store.AddEntryAsync(entry);

        Assert.True(firstAdded);
        Assert.False(replayAdded);
        Assert.Single(await store.GetEntriesAsync(take: 10));
    }

    [Fact]
    public async Task AddEntryAsync_AllowsIdenticalSubmissionAfterDuplicateWindow()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ManualTimeProvider timeProvider = new(
            new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
        FileGuestbookStore store = CreateStore(
            temporaryDirectory.GetPath("guestbook.jsonl"),
            timeProvider);
        GuestbookEntry entry = new()
        {
            Name = "Kyle",
            Message = "Please repeat"
        };

        await store.AddEntryAsync(entry);
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        bool added = await store.AddEntryAsync(entry);

        Assert.True(added);
        Assert.Equal(2, (await store.GetEntriesAsync(take: 10)).Count);
    }

    [Fact]
    public async Task AddEntryAsync_NormalizesNameCasingAndSurroundingWhitespaceForDuplicates()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileGuestbookStore store = CreateStore(
            temporaryDirectory.GetPath("guestbook.jsonl"));

        await store.AddEntryAsync(new GuestbookEntry
        {
            Name = "  KyLe  ",
            Message = "  Same message  "
        });
        bool added = await store.AddEntryAsync(new GuestbookEntry
        {
            Name = "kyle",
            Message = "Same message"
        });

        Assert.False(added);
        Assert.Single(await store.GetEntriesAsync(take: 10));
    }

    [Fact]
    public async Task Constructor_ResolvesRelativeDataPathAgainstAppContextBaseDirectory()
    {
        string relativeDirectory = Path.Combine(
            "guestbook-tests",
            Guid.NewGuid().ToString("N"));
        string relativePath = Path.Combine(relativeDirectory, "guestbook.jsonl");
        string expectedPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, relativePath));
        string currentDirectoryPath = Path.GetFullPath(relativePath);

        try
        {
            FileGuestbookStore store = CreateStore(relativePath);

            await store.AddEntryAsync(new GuestbookEntry
            {
                Message = "Stored beside the application"
            });

            Assert.True(File.Exists(expectedPath));
            if (!string.Equals(
                expectedPath,
                currentDirectoryPath,
                StringComparison.OrdinalIgnoreCase))
            {
                Assert.False(File.Exists(currentDirectoryPath));
            }
        }
        finally
        {
            string cleanupPath = Path.Combine(
                AppContext.BaseDirectory,
                relativeDirectory);
            if (Directory.Exists(cleanupPath))
            {
                Directory.Delete(cleanupPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Constructor_PreservesAbsoluteDataPath()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string absolutePath = Path.GetFullPath(
            temporaryDirectory.GetPath("explicit.jsonl"));
        FileGuestbookStore store = CreateStore(absolutePath);

        await store.AddEntryAsync(new GuestbookEntry
        {
            Message = "Absolute"
        });

        Assert.True(File.Exists(absolutePath));
        Assert.Single(await File.ReadAllLinesAsync(absolutePath));
    }

    private static FileGuestbookStore CreateStore(
        string dataPath,
        TimeProvider? timeProvider = null) =>
        new(
            Options.Create(new GuestbookOptions
            {
                Enabled = true,
                DataPath = dataPath
            }),
            NullLogger<FileGuestbookStore>.Instance,
            timeProvider ?? TimeProvider.System);

    private sealed class ManualTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan elapsed) => utcNow += elapsed;
    }
}
