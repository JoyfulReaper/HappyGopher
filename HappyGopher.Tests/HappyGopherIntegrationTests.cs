/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HappyGopher.Tests;

public sealed class HappyGopherIntegrationTests
{
    private sealed class NullMissionControlClient : IMissionControlClient
    {
        public static NullMissionControlClient Instance { get; } = new();

        private NullMissionControlClient()
        {
        }

        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private static readonly Encoding WireEncoding = new UTF8Encoding(false);

    [Fact]
    public async Task Server_ReturnsRootSelectorResponse()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync();
        server.Content.WriteText("gophermap", "iRoot");

        string response = await server.RequestAsync(string.Empty);

        Assert.Equal("iRoot\tfake\t(NULL)\t0\r\n.\r\n", response);
    }

    [Fact]
    public async Task Server_ReturnsTextFileResponse()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync();
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);
    }

    [Fact]
    public async Task Server_ReturnsMissingSelectorResponse()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync();

        string response = await server.RequestAsync("/missing.txt");

        Assert.Contains("3Selector not found.\terror\t127.0.0.1\t", response);
        Assert.EndsWith(".\r\n", response);
    }

    [Fact]
    public async Task Server_HandlesMultipleConcurrentClients()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync();
        server.Content.WriteText("about.txt", "About");

        Task<string>[] requests = Enumerable.Range(0, 8)
            .Select(_ => server.RequestAsync("/about.txt"))
            .ToArray();

        string[] responses = await Task.WhenAll(requests);

        Assert.All(responses, response => Assert.Equal("About\r\n.\r\n", response));
    }

    [Fact]
    public async Task Server_StopsGracefullyWhileClientConnectionExists()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync();
        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, server.Port);

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        await server.StopAsync(timeout.Token);
    }

    private sealed class TestGopherServer : IAsyncDisposable
    {
        private readonly HappyGopherWorker _worker;
        private bool _stopped;

        private TestGopherServer(
            TestContentStore content,
            int port,
            HappyGopherWorker worker)
        {
            Content = content;
            Port = port;
            _worker = worker;
        }

        public TestContentStore Content { get; }
        public int Port { get; }

        public static async Task<TestGopherServer> StartAsync()
        {
            TestContentStore content = new();
            int port = GetAvailablePort();
            HappyGopherOptions options = new()
            {
                ListenAddress = "127.0.0.1",
                PublicHost = "127.0.0.1",
                Port = port,
                ContentRoot = content.Root,
                RequestTimeoutSeconds = 5
            };

            GopherContentStore store = new(
                Options.Create(options),
                NullLogger<GopherContentStore>.Instance);
            HappyGopherWorker worker = new(
                NullLogger<HappyGopherWorker>.Instance,
                Options.Create(options),
                store,
                NullMissionControlClient.Instance);
            await worker.StartAsync(CancellationToken.None);
            await WaitForServerAsync(port);
            return new TestGopherServer(content, port, worker);
        }

        public async Task<string> RequestAsync(string selector)
        {
            using TcpClient client = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(IPAddress.Loopback, Port, timeout.Token);

            await using NetworkStream stream = client.GetStream();
            byte[] request = WireEncoding.GetBytes(selector + "\r\n");
            await stream.WriteAsync(request, timeout.Token);
            await stream.FlushAsync(timeout.Token);
            client.Client.Shutdown(SocketShutdown.Send);

            using MemoryStream response = new();
            byte[] buffer = new byte[4096];
            while (true)
            {
                int read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                {
                    break;
                }

                response.Write(buffer, 0, read);
            }

            return WireEncoding.GetString(response.ToArray());
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            await _worker.StopAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            await StopAsync(timeout.Token);
            Content.Dispose();
            _worker.Dispose();
        }

        private static int GetAvailablePort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task WaitForServerAsync(int port)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using TcpClient client = new();
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));
                    await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                    return;
                }
                catch (SocketException exception)
                {
                    lastException = exception;
                    await Task.Delay(25);
                }
            }

            throw new InvalidOperationException(
                $"Test server did not start on port {port}.",
                lastException);
        }
    }
}
