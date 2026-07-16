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
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FalseMissionControlClient : IMissionControlClient
    {
        public static FalseMissionControlClient Instance { get; } = new();

        private FalseMissionControlClient()
        {
        }

        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class ThrowingMissionControlClient : IMissionControlClient
    {
        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Telemetry failure");
    }

    private static readonly Encoding WireEncoding = new UTF8Encoding(false);

    [Fact]
    public async Task Server_DoesNotPublishTelemetryForIgnoredRemoteAddress()
    {
        RecordingMissionControlClient recording =
            new();

        await using TestGopherServer server =
            await TestGopherServer.StartAsync(
                missionControlClient: recording,
                telemetryIgnoredRemoteAddress:
                    IPAddress.Loopback.ToString());

        server.Content.WriteText(
            "gophermap",
            "iRoot");

        string response =
            await server.RequestAsync(string.Empty);

        Assert.Equal(
            "iRoot\tfake\t(NULL)\t0\r\n.\r\n",
            response);

        Assert.Empty(recording.PublishedEvents);
    }

    [Fact]
    public async Task Server_ReturnsRootSelectorResponse()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync();
        server.Content.WriteText("gophermap", "iRoot");

        string response = await server.RequestAsync(string.Empty);

        Assert.Equal("iRoot\tfake\t(NULL)\t0\r\n.\r\n", response);
    }

    [Fact]
    public async Task Server_PublishesRootSelectorTelemetry()
    {
        RecordingMissionControlClient recording = new();
        await using TestGopherServer server = await TestGopherServer.StartAsync(recording);
        server.Content.WriteText("gophermap", "iRoot");

        string response = await server.RequestAsync(string.Empty);

        Assert.Equal("iRoot\tfake\t(NULL)\t0\r\n.\r\n", response);

        await recording.WaitForPublishedEventCountAsync(1);

        RecordedMissionControlEvent telemetry = Assert.Single(recording.PublishedEvents);
        Assert.Equal("happygopher.selector.served", telemetry.EventType);
        Assert.NotEqual(default, telemetry.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(telemetry.CorrelationId));

        SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
        Assert.Equal(string.Empty, payload.Selector);
        Assert.Equal("menu", payload.ResponseType);
        Assert.True(payload.Succeeded);
        Assert.True(payload.DurationMilliseconds >= 0);
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
    public async Task Server_PublishesTextFileTelemetry()
    {
        RecordingMissionControlClient recording = new();
        await using TestGopherServer server = await TestGopherServer.StartAsync(recording);
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);

        await recording.WaitForPublishedEventCountAsync(1);

        RecordedMissionControlEvent telemetry = Assert.Single(recording.PublishedEvents);
        SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
        Assert.Equal("/about.txt", payload.Selector);
        Assert.Equal("text", payload.ResponseType);
        Assert.True(payload.Succeeded);
    }

    [Fact]
    public async Task Server_PublishesDirectoryTelemetry()
    {
        RecordingMissionControlClient recording = new();
        await using TestGopherServer server = await TestGopherServer.StartAsync(recording);
        server.Content.WriteText("downloads/gophermap", "iDownloads");

        string response = await server.RequestAsync("/downloads");

        Assert.Contains("iDownloads", response);

        await recording.WaitForPublishedEventCountAsync(1);

        RecordedMissionControlEvent telemetry = Assert.Single(recording.PublishedEvents);
        SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
        Assert.Equal("/downloads", payload.Selector);
        Assert.Equal("menu", payload.ResponseType);
        Assert.True(payload.Succeeded);
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
    public async Task Server_PublishesMissingSelectorTelemetry()
    {
        RecordingMissionControlClient recording = new();
        await using TestGopherServer server = await TestGopherServer.StartAsync(recording);

        string response = await server.RequestAsync("/missing.txt");

        Assert.Contains("3Selector not found.\terror\t127.0.0.1\t", response);
        Assert.EndsWith(".\r\n", response);

        await recording.WaitForPublishedEventCountAsync(1);

        RecordedMissionControlEvent telemetry = Assert.Single(recording.PublishedEvents);
        Assert.Equal("happygopher.selector.served", telemetry.EventType);

        SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
        Assert.Equal("/missing.txt", payload.Selector);
        Assert.Equal("not-found", payload.ResponseType);
        Assert.False(payload.Succeeded);
    }

    [Fact]
    public async Task Server_HandlesMultipleConcurrentClients()
    {
        RecordingMissionControlClient recording = new();
        await using TestGopherServer server = await TestGopherServer.StartAsync(recording);
        server.Content.WriteText("about.txt", "About");

        Task<string>[] requests = Enumerable.Range(0, 8)
            .Select(_ => server.RequestAsync("/about.txt"))
            .ToArray();

        string[] responses = await Task.WhenAll(requests);
        await recording.WaitForPublishedEventCountAsync(requests.Length);

        Assert.All(responses, response => Assert.Equal("About\r\n.\r\n", response));
        Assert.Equal(requests.Length, recording.PublishedEvents.Count);
        Assert.All(recording.PublishedEvents, telemetry =>
        {
            Assert.Equal("happygopher.selector.served", telemetry.EventType);
            SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
            Assert.Equal("/about.txt", payload.Selector);
            Assert.Equal("text", payload.ResponseType);
            Assert.True(payload.Succeeded);
        });
    }

    [Fact]
    public async Task Server_IgnoresMissionControlClientReturningFalse()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync(FalseMissionControlClient.Instance);
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);
    }

    [Fact]
    public async Task Server_IgnoresMissionControlClientThrowing()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync(new ThrowingMissionControlClient());
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);
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
            HappyGopherWorker worker,
            IMissionControlClient missionControlClient)
        {
            Content = content;
            Port = port;
            _worker = worker;
            MissionControlClient = missionControlClient;
        }

        public TestContentStore Content { get; }
        public int Port { get; }
        public IMissionControlClient MissionControlClient { get; }

        public static async Task<TestGopherServer> StartAsync(
            IMissionControlClient? missionControlClient = null,
            string? telemetryIgnoredRemoteAddress = null)
        {
            TestContentStore content = new();
            int port = GetAvailablePort();
            HappyGopherOptions options = new()
            {
                ListenAddress = "127.0.0.1",
                PublicHost = "127.0.0.1",
                Port = port,
                ContentRoot = content.Root,
                RequestTimeoutSeconds = 5,
                TelemetryIgnoredRemoteAddress =
                    telemetryIgnoredRemoteAddress
            };

            missionControlClient ??= NullMissionControlClient.Instance;

            GopherContentStore store = new(
                Options.Create(options),
                NullLogger<GopherContentStore>.Instance);
            HappyGopherWorker worker = new(
                NullLogger<HappyGopherWorker>.Instance,
                Options.Create(options),
                store,
                missionControlClient);
            await worker.StartAsync(CancellationToken.None);
            await WaitForServerAsync(port);
            return new TestGopherServer(content, port, worker, missionControlClient);
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
