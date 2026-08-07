using HappyGopher.Abstractions;
using HappyGopher.Gopher;
using HappyGopher.Pages.Guestbook;
using Microsoft.Extensions.Options;
using System.Text;

namespace HappyGopher.Tests;

public sealed class ViewGuestbookPageTests
{
    [Fact]
    public async Task WriteAsync_DisplaysAnonymousForNullOrBlankNames()
    {
        StubGuestbookStore store = new(
        [
            new GuestbookEntry { Name = null, Message = "Null name" },
            new GuestbookEntry { Name = "   ", Message = "Blank name" }
        ]);
        ViewGuestbookPage page = CreatePage(store);

        string response = await WriteAsync(page);

        Assert.Equal(2, CountOccurrences(response, "iAnonymous\tfake\t(NULL)\t0\r\n"));
        Assert.Contains("i  Null name\tfake\t(NULL)\t0\r\n", response);
        Assert.Contains("i  Blank name\tfake\t(NULL)\t0\r\n", response);
    }

    [Fact]
    public async Task WriteAsync_DisplaysEntriesNewestFirst()
    {
        StubGuestbookStore store = new(
        [
            new GuestbookEntry { Name = "Oldest", Message = "First" },
            new GuestbookEntry { Name = "Middle", Message = "Second" },
            new GuestbookEntry { Name = "Newest", Message = "Third" }
        ]);
        ViewGuestbookPage page = CreatePage(store);

        string response = await WriteAsync(page);

        int newestIndex = response.IndexOf("iNewest\t", StringComparison.Ordinal);
        int middleIndex = response.IndexOf("iMiddle\t", StringComparison.Ordinal);
        int oldestIndex = response.IndexOf("iOldest\t", StringComparison.Ordinal);
        Assert.True(newestIndex < middleIndex);
        Assert.True(middleIndex < oldestIndex);
    }

    [Fact]
    public async Task WriteAsync_UsesConfiguredMaximumEntryCount()
    {
        StubGuestbookStore store = new([]);
        ViewGuestbookPage page = CreatePage(
            store,
            maxEntriesDisplayed: 17);

        await WriteAsync(page);

        Assert.Equal(17, store.RequestedTake);
        Assert.Equal(1, store.GetCallCount);
    }

    [Fact]
    public async Task WriteAsync_WritesSignAndRootLinksAndCompletesResponse()
    {
        StubGuestbookStore store = new([]);
        ViewGuestbookPage page = CreatePage(store);

        string response = await WriteAsync(page);

        Assert.Contains(
            "7Sign the guestbook\t/guestbook/sign\tgopher.test\t7070\r\n",
            response);
        Assert.Contains(
            "1Go back to main menu\t\tgopher.test\t7070\r\n",
            response);
        Assert.EndsWith(".\r\n", response);
    }

    private static ViewGuestbookPage CreatePage(
        StubGuestbookStore store,
        int maxEntriesDisplayed = 50) =>
        new(
            store,
            Options.Create(new GuestbookOptions
            {
                Enabled = true,
                MaxEntriesDisplayed = maxEntriesDisplayed
            }),
            Options.Create(new HappyGopherOptions
            {
                PublicHost = "gopher.test",
                Port = 7070
            }));

    private static async Task<string> WriteAsync(ViewGuestbookPage page)
    {
        await using MemoryStream output = new();
        GopherResponseKind responseKind = await page.WriteAsync(
            new GopherRequest(page.Selector, Input: null),
            output,
            CancellationToken.None);

        Assert.Equal(GopherResponseKind.Menu, responseKind);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static int CountOccurrences(string value, string expected)
    {
        int count = 0;
        int startIndex = 0;
        while ((startIndex = value.IndexOf(
            expected,
            startIndex,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += expected.Length;
        }

        return count;
    }

    private sealed class StubGuestbookStore(
        IReadOnlyList<GuestbookEntry> entries) : IGuestbookStore
    {
        public int GetCallCount { get; private set; }
        public int? RequestedTake { get; private set; }

        public Task<bool> AddEntryAsync(
            GuestbookEntry entry,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<GuestbookEntry>> GetEntriesAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            RequestedTake = take;
            return Task.FromResult(entries);
        }
    }
}
