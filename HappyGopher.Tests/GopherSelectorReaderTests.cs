/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HappyGopher.Tests;

public sealed class GopherSelectorReaderTests
{
    private const int MaxSelectorBytes = 12;
    private static readonly Encoding WireEncoding = new UTF8Encoding(false);

    [Fact]
    public async Task ReadAsync_ReturnsSelectorReceivedInOneWrite()
    {
        string? selector = await ReadFromBytesAsync("/about.txt\r\n");

        Assert.Equal("/about.txt", selector);
    }

    [Fact]
    public async Task ReadAsync_ReturnsSelectorSplitAcrossMultipleWrites()
    {
        string? selector = await ReadFromTcpWritesAsync("/down", "loads\r\n");

        Assert.Equal("/downloads", selector);
    }

    [Fact]
    public async Task ReadAsync_AcceptsLfOnlyRequest()
    {
        string? selector = await ReadFromBytesAsync("/about.txt\n");

        Assert.Equal("/about.txt", selector);
    }

    [Fact]
    public async Task ReadAsync_ReturnsEmptySelectorForEmptyLine()
    {
        string? selector = await ReadFromBytesAsync("\r\n");

        Assert.Equal(string.Empty, selector);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenClientClosesWithoutSendingAnything()
    {
        string? selector = await GopherSelectorReader.ReadAsync(
            new MemoryStream(),
            MaxSelectorBytes,
            requestTimeoutSeconds: 5,
            CancellationToken.None);

        Assert.Null(selector);
    }

    [Fact]
    public async Task ReadAsync_ReturnsSelectorWhenClientClosesWithoutNewline()
    {
        string? selector = await ReadFromBytesAsync("/about.txt");

        Assert.Equal("/about.txt", selector);
    }

    [Fact]
    public async Task ReadAsync_AcceptsSelectorExactlyAtByteLimitFollowedByCrLf()
    {
        string selector = new('a', MaxSelectorBytes);

        string? result = await ReadFromBytesAsync(selector + "\r\n");

        Assert.Equal(selector, result);
    }

    [Fact]
    public async Task ReadAsync_RejectsSelectorOneByteLargerThanByteLimit()
    {
        string selector = new('a', MaxSelectorBytes + 1);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadFromBytesAsync(selector));
    }

    [Fact]
    public async Task ReadAsync_AcceptsSelectorExactlyAtByteLimitFollowedByLf()
    {
        string selector = new('a', MaxSelectorBytes);

        string? result = await ReadFromBytesAsync(selector + "\n");

        Assert.Equal(selector, result);
    }

    [Fact]
    public async Task ReadAsync_AppliesLimitToUtf8BytesNotCharacterCount()
    {
        string selector = "éééééé";

        Assert.Equal(MaxSelectorBytes, WireEncoding.GetByteCount(selector));

        string? result = await ReadFromBytesAsync(selector + "\n");

        Assert.Equal(selector, result);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadFromBytesAsync(selector + "a\n"));
    }

    [Fact]
    public async Task ReadAsync_TimesOutWhenClientSendsNoCompleteRequest()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ReadFromOpenTcpClientAsync());
    }

    private static Task<string?> ReadFromBytesAsync(string value) =>
        GopherSelectorReader.ReadAsync(
            new MemoryStream(WireEncoding.GetBytes(value)),
            MaxSelectorBytes,
            requestTimeoutSeconds: 5,
            CancellationToken.None);

    private static async Task<string?> ReadFromTcpWritesAsync(
        params string[] writes)
    {
        await using TcpPair pair = await TcpPair.CreateAsync();
        Task<string?> readTask = GopherSelectorReader.ReadAsync(
            pair.ServerStream,
            MaxSelectorBytes,
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

    private static async Task<string?> ReadFromOpenTcpClientAsync()
    {
        await using TcpPair pair = await TcpPair.CreateAsync();
        return await GopherSelectorReader.ReadAsync(
            pair.ServerStream,
            MaxSelectorBytes,
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
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(IPAddress.Loopback, port);
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
