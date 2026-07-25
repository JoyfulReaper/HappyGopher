/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Events;
using HappyGopher.Pages;
using JoyfulReaperLib.MissionControl;
using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            CancellationToken cancellationToken = default)
        {
            if (eventType != GopherServiceStartedEvent.EventName)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    private sealed class ThrowingMissionControlClient : IMissionControlClient
    {
        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            if (eventType != GopherServiceStartedEvent.EventName)
            {
                return Task.FromResult(true);
            }

            throw new InvalidOperationException("Telemetry failure");
        }
    }

    private sealed class StartupTimeoutMissionControlClient :
        IMissionControlClient
    {
        private int _canceledCount;

        public int CanceledCount => Volatile.Read(ref _canceledCount);

        public async Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            if (eventType != GopherServiceStartedEvent.EventName)
            {
                return true;
            }

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _canceledCount);
                throw;
            }

            return true;
        }
    }

    private sealed class BlockingMissionControlClient : IMissionControlClient
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _startedSignal = new(0);
        private readonly SemaphoreSlim _finishedSignal = new(0);
        private readonly bool _throwAfterRelease;
        private readonly bool _publishResult;
        private int _startedCount;
        private int _finishedCount;
        private int _canceledCount;

        public BlockingMissionControlClient(
            bool throwAfterRelease = false,
            bool publishResult = true)
        {
            _throwAfterRelease = throwAfterRelease;
            _publishResult = publishResult;
        }

        public int StartedCount => Volatile.Read(ref _startedCount);
        public int FinishedCount => Volatile.Read(ref _finishedCount);
        public int CanceledCount => Volatile.Read(ref _canceledCount);

        public async Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            if (eventType != SelectorServedEventName)
            {
                return true;
            }

            Interlocked.Increment(ref _startedCount);
            _startedSignal.Release();

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _canceledCount);
                throw;
            }
            finally
            {
                Interlocked.Increment(ref _finishedCount);
                _finishedSignal.Release();
            }

            if (_throwAfterRelease)
            {
                throw new InvalidOperationException("Telemetry failure");
            }

            return _publishResult;
        }

        public void Release() =>
            _release.TrySetResult();

        public async Task WaitForStartedCountAsync(
            int expectedCount,
            CancellationToken cancellationToken)
        {
            while (StartedCount < expectedCount)
            {
                await _startedSignal.WaitAsync(cancellationToken);
            }
        }

        public async Task WaitForFinishedCountAsync(
            int expectedCount,
            CancellationToken cancellationToken)
        {
            while (FinishedCount < expectedCount)
            {
                await _finishedSignal.WaitAsync(cancellationToken);
            }
        }
    }

    private const string SelectorServedEventName =
        "happygopher.selector.served";

    private static readonly Encoding WireEncoding = new UTF8Encoding(false);

    [Fact]
    public async Task Server_PublishesStartupTelemetry()
    {
        RecordingMissionControlClient recording = new();

        await using TestGopherServer server =
            await TestGopherServer.StartAsync(recording);

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await recording.WaitForPublishedEventCountAsync(GopherServiceStartedEvent.EventName, 1, timeout.Token);

        RecordedMissionControlEvent telemetry = Assert.Single(
            recording.PublishedEvents,
            publishedEvent =>
                publishedEvent.EventType ==
                GopherServiceStartedEvent.EventName);

        Assert.Equal(
            GopherServiceStartedEvent.EventName,
            telemetry.EventType);
        Assert.NotEqual(default, telemetry.OccurredAt);
        Assert.Null(telemetry.CorrelationId);

        GopherServiceStartedEvent payload =
            Assert.IsType<GopherServiceStartedEvent>(
                telemetry.Payload);
        Assert.Equal(
            $"127.0.0.1:{server.Port}",
            payload.ListenAddress);
    }

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

        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(5));

        await Task.Delay(
            TimeSpan.FromMilliseconds(250),
            timeout.Token);

        Assert.DoesNotContain(
            recording.PublishedEvents,
            publishedEvent =>
                publishedEvent.EventType ==
                SelectorServedEventName);
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

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await recording.WaitForPublishedEventCountAsync(GopherServiceStartedEvent.EventName, 1, timeout.Token);

        RecordedMissionControlEvent telemetry = Assert.Single(
            recording.PublishedEvents,
            publishedEvent =>
                publishedEvent.EventType ==
                SelectorServedEventName);
        Assert.Equal(SelectorServedEventName, telemetry.EventType);
        Assert.NotEqual(default, telemetry.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(telemetry.CorrelationId));

        SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
        Assert.Equal(string.Empty, payload.Selector);
        Assert.Equal("menu", payload.ResponseType);
        Assert.True(payload.Succeeded);
        Assert.True(payload.DurationMilliseconds >= 0);
    }

    [Fact]
    public async Task Server_SendsEofBeforeTelemetryCompletes()
    {
        BlockingMissionControlClient blocking = new();
        await using TestGopherServer server = await TestGopherServer.StartAsync(blocking);
        server.Content.WriteText("gophermap", "iRoot");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        string response = await server.RequestAsync(string.Empty);
        await blocking.WaitForStartedCountAsync(1, timeout.Token);

        Assert.Equal("iRoot\tfake\t(NULL)\t0\r\n.\r\n", response);
        Assert.Equal(0, blocking.FinishedCount);

        blocking.Release();
        await blocking.WaitForFinishedCountAsync(1, timeout.Token);
    }

    [Fact]
    public async Task Server_ReleasesConnectionSlotBeforeTelemetryCompletes()
    {
        BlockingMissionControlClient blocking = new();
        await using TestGopherServer server =
            await TestGopherServer.StartAsync(
                missionControlClient: blocking,
                maxConcurrentConnections: 1);
        server.Content.WriteText("about.txt", "About");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        string firstResponse = await server.RequestAsync("/about.txt");
        await blocking.WaitForStartedCountAsync(1, timeout.Token);
        Assert.Equal(0, blocking.FinishedCount);

        string secondResponse = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", firstResponse);
        Assert.Equal("About\r\n.\r\n", secondResponse);

        blocking.Release();
        await blocking.WaitForFinishedCountAsync(2, timeout.Token);
    }

    [Fact]
    public async Task Server_HoldsConnectionSlotWhileWaitingForSelector()
    {
        await using TestGopherServer server =
            await TestGopherServer.StartAsync(maxConcurrentConnections: 1);
        server.Content.WriteText("about.txt", "About");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        using TcpClient firstClient = new();
        await firstClient.ConnectAsync(
            IPAddress.Loopback,
            server.Port,
            timeout.Token);

        await using NetworkStream firstStream = firstClient.GetStream();
        await firstStream.WriteAsync(
            WireEncoding.GetBytes("/incomplete"),
            timeout.Token);

        Task<string> secondRequest = server.RequestAsync("/about.txt");

        await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        Assert.False(secondRequest.IsCompleted);

        firstClient.Dispose();

        string secondResponse = await secondRequest.WaitAsync(timeout.Token);
        Assert.Equal("About\r\n.\r\n", secondResponse);
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

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await recording.WaitForPublishedEventCountAsync(GopherServiceStartedEvent.EventName, 1, timeout.Token);

        RecordedMissionControlEvent telemetry = Assert.Single(
            recording.PublishedEvents,
            publishedEvent =>
                publishedEvent.EventType ==
                SelectorServedEventName);
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

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await recording.WaitForPublishedEventCountAsync(GopherServiceStartedEvent.EventName, 1, timeout.Token);

        RecordedMissionControlEvent telemetry = Assert.Single(
            recording.PublishedEvents,
            publishedEvent =>
                publishedEvent.EventType ==
                SelectorServedEventName);
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

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await recording.WaitForPublishedEventCountAsync(GopherServiceStartedEvent.EventName, 1, timeout.Token);

        RecordedMissionControlEvent telemetry = Assert.Single(
            recording.PublishedEvents,
            publishedEvent =>
                publishedEvent.EventType ==
                SelectorServedEventName);
        Assert.Equal(SelectorServedEventName, telemetry.EventType);

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
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await recording.WaitForPublishedEventCountAsync(GopherServiceStartedEvent.EventName, 1, timeout.Token);

        RecordedMissionControlEvent[] selectorTelemetry =
            recording.PublishedEvents
                .Where(
                    publishedEvent =>
                        publishedEvent.EventType ==
                        SelectorServedEventName)
                .ToArray();

        Assert.All(responses, response => Assert.Equal("About\r\n.\r\n", response));
        Assert.Equal(requests.Length, selectorTelemetry.Length);
        Assert.All(selectorTelemetry, telemetry =>
        {
            Assert.Equal(SelectorServedEventName, telemetry.EventType);
            SelectorServedEvent payload = Assert.IsType<SelectorServedEvent>(telemetry.Payload);
            Assert.Equal("/about.txt", payload.Selector);
            Assert.Equal("text", payload.ResponseType);
            Assert.True(payload.Succeeded);
        });
    }

    [Fact]
    public async Task Server_StartupTelemetryReturningFalseDoesNotPreventRequests()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync(FalseMissionControlClient.Instance);
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);
    }

    [Fact]
    public async Task Server_StartupTelemetryThrowingDoesNotPreventRequests()
    {
        await using TestGopherServer server = await TestGopherServer.StartAsync(new ThrowingMissionControlClient());
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);
    }

    [Fact]
    public async Task Server_StartupTelemetryTimingOutDoesNotPreventRequests()
    {
        StartupTimeoutMissionControlClient timeoutClient = new();
        await using TestGopherServer server =
            await TestGopherServer.StartAsync(timeoutClient);
        server.Content.WriteText("about.txt", "About");

        string response = await server.RequestAsync("/about.txt");

        Assert.Equal("About\r\n.\r\n", response);
        Assert.Equal(1, timeoutClient.CanceledCount);
    }

    [Fact]
    public async Task Server_ContinuesServingAfterTelemetryException()
    {
        BlockingMissionControlClient blocking = new(throwAfterRelease: true);
        await using TestGopherServer server = await TestGopherServer.StartAsync(blocking);
        server.Content.WriteText("about.txt", "About");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        string firstResponse = await server.RequestAsync("/about.txt");
        await blocking.WaitForStartedCountAsync(1, timeout.Token);

        blocking.Release();
        await blocking.WaitForFinishedCountAsync(1, timeout.Token);

        string secondResponse = await server.RequestAsync("/about.txt");
        await blocking.WaitForStartedCountAsync(2, timeout.Token);

        Assert.Equal("About\r\n.\r\n", firstResponse);
        Assert.Equal("About\r\n.\r\n", secondResponse);
    }

    [Fact]
    public async Task Server_TelemetryTimeoutDoesNotHangHandler()
    {
        BlockingMissionControlClient blocking = new();
        await using TestGopherServer server =
            await TestGopherServer.StartAsync(
                missionControlClient: blocking,
                maxConcurrentConnections: 1);
        server.Content.WriteText("about.txt", "About");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        string response = await server.RequestAsync("/about.txt");
        await blocking.WaitForStartedCountAsync(1, timeout.Token);
        await blocking.WaitForFinishedCountAsync(1, timeout.Token);

        Assert.Equal("About\r\n.\r\n", response);
        Assert.Equal(1, blocking.CanceledCount);
    }

    [Fact]
    public async Task Server_DoesNotLeakConnectionSlotAfterClientDisconnect()
    {
        await using TestGopherServer server =
            await TestGopherServer.StartAsync(maxConcurrentConnections: 1);
        server.Content.WriteText("about.txt", "About");

        using TcpClient client = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        client.Dispose();

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
        private readonly IHost _host;
        private bool _stopped;

        private TestGopherServer(
            TestContentStore content,
            int port,
            IHost host,
            IMissionControlClient missionControlClient)
        {
            Content = content;
            Port = port;
            _host = host;
            MissionControlClient = missionControlClient;
        }

        public TestContentStore Content { get; }
        public int Port { get; }
        public IMissionControlClient MissionControlClient { get; }

        public static async Task<TestGopherServer> StartAsync(
            IMissionControlClient? missionControlClient = null,
            string? telemetryIgnoredRemoteAddress = null,
            int maxConcurrentConnections = 64)
        {
            TestContentStore content = new();
            int port = GetAvailablePort();
            HappyGopherOptions options = new()
            {
                ListenAddress = "127.0.0.1",
                PublicHost = "127.0.0.1",
                Port = port,
                ContentRoot = content.Root,
                MaxConcurrentConnections = maxConcurrentConnections,
                RequestTimeoutSeconds = 5,
                TelemetryIgnoredRemoteAddress =
                    telemetryIgnoredRemoteAddress
            };

            missionControlClient ??= NullMissionControlClient.Instance;

            IHost? host = null;

            try
            {
                host = Host.CreateDefaultBuilder()
                    .UseDefaultServiceProvider(options =>
                    {
                        options.ValidateOnBuild = true;
                        options.ValidateScopes = true;
                    })
                    .ConfigureLogging(logging =>
                        logging.ClearProviders())
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(missionControlClient);
                        services.AddSingleton<IOptions<HappyGopherOptions>>(
                            Options.Create(options));
                        services.AddSingleton<GopherContentStore>();
                        services.AddScoped<GopherPageResolver>();
                        services.AddTcpServer<
                            GopherConnectionHandler,
                            HappyGopherOptions>();
                        services.AddHostedService<GopherLifecycleService>();
                    })
                    .Build();

                using CancellationTokenSource timeout =
                    new(TimeSpan.FromSeconds(5));
                await host.StartAsync(timeout.Token);

                return new TestGopherServer(
                    content,
                    port,
                    host,
                    missionControlClient);
            }
            catch
            {
                try
                {
                    host?.Dispose();
                }
                finally
                {
                    content.Dispose();
                }

                throw;
            }
        }

        public async Task<string> RequestAsync(string selector)
        {
            using TcpClient client = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(IPAddress.Loopback, Port, timeout.Token);

            await using NetworkStream stream = client.GetStream();
            byte[] request = WireEncoding.GetBytes(selector + "\r\n");
            await stream.WriteAsync(request, timeout.Token);
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
            await _host.StopAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using CancellationTokenSource timeout =
                    new(TimeSpan.FromSeconds(5));
                await StopAsync(timeout.Token);
            }
            finally
            {
                try
                {
                    _host.Dispose();
                }
                finally
                {
                    Content.Dispose();
                }
            }
        }

        private static int GetAvailablePort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
