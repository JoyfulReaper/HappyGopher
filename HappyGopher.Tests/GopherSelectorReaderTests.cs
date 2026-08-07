/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using HappyGopher.Gopher;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HappyGopher.Tests;

public sealed class GopherSelectorReaderTests
{
    private const int MaxSelectorBytes = 14;
    private const int MaxInputBytes = 18;
    private static readonly Encoding WireEncoding = new UTF8Encoding(false);

    [Fact]
    public async Task ReadAsync_ReturnsSelectorWithoutInput()
    {
        GopherRequest? request =
            await ReadFromBytesAsync("/about\r\n");

        Assert.NotNull(request);
        Assert.Equal("/about", request.Selector);
        Assert.Null(request.Input);
    }

    [Fact]
    public async Task ReadAsync_ReturnsSelectorWithInput()
    {
        GopherRequest? request =
            await ReadFromBytesAsync(
                "/guestbook/add\tHello from Gopher\r\n");

        Assert.NotNull(request);
        Assert.Equal("/guestbook/add", request.Selector);
        Assert.Equal("Hello from Gopher", request.Input);
    }

    [Fact]
    public async Task ReadAsync_ReturnsEmptyInputAfterTrailingTab()
    {
        GopherRequest? request =
            await ReadFromBytesAsync("/guestbook/add\t\r\n");

        Assert.NotNull(request);
        Assert.Equal("/guestbook/add", request.Selector);
        Assert.Equal(string.Empty, request.Input);
    }

    [Fact]
    public async Task ReadAsync_PreservesAdditionalTabsInInput()
    {
        GopherRequest? request =
            await ReadFromBytesAsync(
                "/search\tone\ttwo\tthree\r\n");

        Assert.NotNull(request);
        Assert.Equal("/search", request.Selector);
        Assert.Equal("one\ttwo\tthree", request.Input);
    }

    [Fact]
    public async Task ReadAsync_ReturnsRequestSplitAcrossMultipleWrites()
    {
        GopherRequest? request =
            await ReadFromTcpWritesAsync(
                "/guestbook/",
                "add\tHello ",
                "from Gopher\r\n");

        Assert.NotNull(request);
        Assert.Equal("/guestbook/add", request.Selector);
        Assert.Equal("Hello from Gopher", request.Input);
    }

    [Fact]
    public async Task ReadAsync_AcceptsLfOnlyRequest()
    {
        GopherRequest? request =
            await ReadFromBytesAsync("/about.txt\n");

        Assert.NotNull(request);
        Assert.Equal("/about.txt", request.Selector);
        Assert.Null(request.Input);
    }

    [Fact]
    public async Task ReadAsync_ReturnsEmptySelectorForEmptyLine()
    {
        GopherRequest? request = await ReadFromBytesAsync("\r\n");

        Assert.NotNull(request);
        Assert.Equal(string.Empty, request.Selector);
        Assert.Null(request.Input);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenClientClosesWithoutSendingAnything()
    {
        GopherRequest? request = await GopherSelectorReader.ReadAsync(
            new MemoryStream(),
            MaxSelectorBytes,
            MaxInputBytes,
            requestTimeoutSeconds: 5,
            CancellationToken.None);

        Assert.Null(request);
    }

    [Fact]
    public async Task ReadAsync_ReturnsRequestWhenClientClosesWithoutNewline()
    {
        GopherRequest? request =
            await ReadFromBytesAsync("/search\tgophers");

        Assert.NotNull(request);
        Assert.Equal("/search", request.Selector);
        Assert.Equal("gophers", request.Input);
    }

    [Fact]
    public async Task ReadAsync_AcceptsSelectorExactlyAtByteLimitFollowedByCrLf()
    {
        string selector = new('a', MaxSelectorBytes);

        GopherRequest? request =
            await ReadFromBytesAsync(selector + "\r\n");

        Assert.NotNull(request);
        Assert.Equal(selector, request.Selector);
    }

    [Fact]
    public async Task ReadAsync_AcceptsSelectorExactlyAtByteLimitFollowedByLf()
    {
        string selector = new('a', MaxSelectorBytes);

        GopherRequest? request =
            await ReadFromBytesAsync(selector + "\n");

        Assert.NotNull(request);
        Assert.Equal(selector, request.Selector);
    }

    [Fact]
    public async Task ReadAsync_RejectsSelectorOneByteLargerThanByteLimit()
    {
        string selector = new('a', MaxSelectorBytes + 1);

        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => ReadFromBytesAsync(selector));

        Assert.Contains("Selector", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_AcceptsInputExactlyAtByteLimit()
    {
        string input = new('a', MaxInputBytes);

        GopherRequest? request =
            await ReadFromBytesAsync("/search\t" + input + "\r\n");

        Assert.NotNull(request);
        Assert.Equal(input, request.Input);
    }

    [Fact]
    public async Task ReadAsync_RejectsInputOneByteLargerThanByteLimit()
    {
        string input = new('a', MaxInputBytes + 1);

        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => ReadFromBytesAsync("/search\t" + input));

        Assert.Contains("Input", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_AppliesSelectorLimitToUtf8ByteCount()
    {
        string selector = "ééééééé";

        Assert.Equal(
            MaxSelectorBytes,
            WireEncoding.GetByteCount(selector));

        GopherRequest? request =
            await ReadFromBytesAsync(selector + "\n");

        Assert.NotNull(request);
        Assert.Equal(selector, request.Selector);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadFromBytesAsync(selector + "a\n"));
    }

    [Fact]
    public async Task ReadAsync_AppliesInputLimitToUtf8ByteCount()
    {
        string input = "ééééééééé";

        Assert.Equal(
            MaxInputBytes,
            WireEncoding.GetByteCount(input));

        GopherRequest? request =
            await ReadFromBytesAsync("/search\t" + input + "\n");

        Assert.NotNull(request);
        Assert.Equal(input, request.Input);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadFromBytesAsync("/search\t" + input + "a\n"));
    }

    [Fact]
    public async Task ReadAsync_TimesOutWhenClientSendsNoCompleteRequest()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ReadFromOpenTcpClientAsync());
    }

    private static Task<GopherRequest?> ReadFromBytesAsync(
        string value) =>
        GopherSelectorReader.ReadAsync(
            new MemoryStream(WireEncoding.GetBytes(value)),
            MaxSelectorBytes,
            MaxInputBytes,
            requestTimeoutSeconds: 5,
            CancellationToken.None);

    private static async Task<GopherRequest?> ReadFromTcpWritesAsync(
        params string[] writes)
    {
        await using TcpPair pair = await TcpPair.CreateAsync();
        Task<GopherRequest?> readTask = GopherSelectorReader.ReadAsync(
            pair.ServerStream,
            MaxSelectorBytes,
            MaxInputBytes,
            requestTimeoutSeconds: 5,
            CancellationToken.None);

        foreach (string write in writes)
        {
            byte[] bytes = WireEncoding.GetBytes(write);
            await pair.ClientStream.WriteAsync(bytes);
            await pair.ClientStream.FlushAsync();
        }

        pair.Client.Client.Shutdown(SocketShutdown.Send);
        return await readTask;
    }

    private static async Task<GopherRequest?>
        ReadFromOpenTcpClientAsync()
    {
        await using TcpPair pair = await TcpPair.CreateAsync();
        return await GopherSelectorReader.ReadAsync(
            pair.ServerStream,
            MaxSelectorBytes,
            MaxInputBytes,
            requestTimeoutSeconds: 1,
            CancellationToken.None);
    }

    private sealed class TcpPair : IAsyncDisposable
    {
        private TcpPair(TcpClient client, TcpClient server)
        {
            Client = client;
            Server = server;
            ClientStream = client.GetStream();
            ServerStream = server.GetStream();
        }

        public TcpClient Client { get; }
        public TcpClient Server { get; }
        public NetworkStream ClientStream { get; }
        public NetworkStream ServerStream { get; }

        public static async Task<TcpPair> CreateAsync()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();

            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            TcpClient client = new();
            Task<TcpClient> acceptTask =
                listener.AcceptTcpClientAsync();
            await client.ConnectAsync(
                IPAddress.Loopback,
                port);
            TcpClient server = await acceptTask;
            listener.Stop();

            return new TcpPair(client, server);
        }

        public async ValueTask DisposeAsync()
        {
            await ServerStream.DisposeAsync();
            await ClientStream.DisposeAsync();
            Server.Dispose();
            Client.Dispose();
        }
    }
}
