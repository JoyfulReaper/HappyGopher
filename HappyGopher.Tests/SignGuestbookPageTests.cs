using HappyGopher.Abstractions;
using HappyGopher.Gopher;
using HappyGopher.Pages.Guestbook;
using Microsoft.Extensions.Options;
using System.Text;

namespace HappyGopher.Tests;

public sealed class SignGuestbookPageTests
{
    [Fact]
    public async Task WriteAsync_AcceptsNameAndMessage()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        (GopherResponseKind responseKind, string response) =
            await WriteAsync(page, "Kyle | Hello there");

        GuestbookEntry entry = Assert.Single(store.AddedEntries);
        Assert.Equal("Kyle", entry.Name);
        Assert.Equal("Hello there", entry.Message);
        Assert.Equal(1, store.AddCallCount);
        Assert.Equal(GopherResponseKind.Menu, responseKind);
        Assert.Contains(
            "1View the guestbook\t/guestbook\tgopher.test\t7070\r\n",
            response);
        Assert.EndsWith(".\r\n", response);
    }

    [Fact]
    public async Task WriteAsync_AcceptsMessageWithoutName()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        await WriteAsync(page, "A message from Anonymous");

        GuestbookEntry entry = Assert.Single(store.AddedEntries);
        Assert.Null(entry.Name);
        Assert.Equal("A message from Anonymous", entry.Message);
        Assert.Equal(1, store.AddCallCount);
    }

    [Fact]
    public async Task WriteAsync_ConvertsEmptyNameToAnonymous()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        (_, string response) = await WriteAsync(page, "   | Hello");

        GuestbookEntry entry = Assert.Single(store.AddedEntries);
        Assert.Null(entry.Name);
        Assert.Equal("Hello", entry.Message);
        Assert.Contains("iThanks, Anonymous.\tfake\t(NULL)\t0\r\n", response);
    }

    [Fact]
    public async Task WriteAsync_RejectsEmptyMessage()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        (_, string response) = await WriteAsync(page, "Kyle |   ");

        Assert.Equal(0, store.AddCallCount);
        Assert.Contains("3A message is required.\terror\tgopher.test\t7070\r\n", response);
        Assert.EndsWith(".\r\n", response);
    }

    [Fact]
    public async Task WriteAsync_RejectsNameLongerThanFortyCharacters()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        (_, string response) = await WriteAsync(
            page,
            $"{new string('N', 41)} | Hello");

        Assert.Equal(0, store.AddCallCount);
        Assert.Contains("3Name cannot exceed 40 characters.", response);
    }

    [Fact]
    public async Task WriteAsync_RejectsMessageLongerThanFiveHundredCharacters()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        (_, string response) = await WriteAsync(
            page,
            new string('M', 501));

        Assert.Equal(0, store.AddCallCount);
        Assert.Contains("3Message cannot exceed 500 characters.", response);
    }

    [Fact]
    public async Task WriteAsync_SanitizesControlCharactersBeforeAddingEntry()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        await WriteAsync(page, "Ky\0le | A\0B\rC\nD\tE");

        GuestbookEntry entry = Assert.Single(store.AddedEntries);
        Assert.Equal("Ky le", entry.Name);
        Assert.Equal("A B C D E", entry.Message);
        Assert.Equal(1, store.AddCallCount);
    }

    [Fact]
    public async Task WriteAsync_CallsStoreExactlyOnceForEachSuccessfulRequest()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store);

        await WriteAsync(page, "First request");
        await WriteAsync(page, "Second request");

        Assert.Equal(2, store.AddCallCount);
        Assert.Equal(2, store.AddedEntries.Count);
    }

    [Fact]
    public async Task WriteAsync_ReturnsCompletedDisabledResponseWithoutCallingStore()
    {
        RecordingGuestbookStore store = new();
        SignGuestbookPage page = CreatePage(store, enabled: false);

        (GopherResponseKind responseKind, string response) =
            await WriteAsync(page, "Kyle | Should not be stored");

        Assert.Equal(GopherResponseKind.InvalidSelector, responseKind);
        Assert.Equal(0, store.AddCallCount);
        Assert.Contains(
            "3The guestbook is currently disabled.\terror\tgopher.test\t7070\r\n",
            response);
        Assert.EndsWith(".\r\n", response);
    }

    private static SignGuestbookPage CreatePage(
        RecordingGuestbookStore store,
        bool enabled = true) =>
        new(
            store,
            Options.Create(new GuestbookOptions
            {
                Enabled = enabled
            }),
            Options.Create(new HappyGopherOptions
            {
                PublicHost = "gopher.test",
                Port = 7070
            }));

    private static async Task<(GopherResponseKind Kind, string Response)>
        WriteAsync(SignGuestbookPage page, string? input)
    {
        await using MemoryStream output = new();
        GopherResponseKind responseKind = await page.WriteAsync(
            new GopherRequest(page.Selector, input),
            output,
            CancellationToken.None);

        return (
            responseKind,
            Encoding.UTF8.GetString(output.ToArray()));
    }

    private sealed class RecordingGuestbookStore : IGuestbookStore
    {
        public List<GuestbookEntry> AddedEntries { get; } = [];
        public int AddCallCount { get; private set; }

        public Task<bool> AddEntryAsync(
            GuestbookEntry entry,
            CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            AddedEntries.Add(entry);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<GuestbookEntry>> GetEntriesAsync(
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GuestbookEntry>>([]);
    }
}
