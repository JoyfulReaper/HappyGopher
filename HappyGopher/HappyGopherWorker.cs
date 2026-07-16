/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRNet;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HappyGopher;

public class HappyGopherWorker(
    ILogger<HappyGopherWorker> logger,
    IOptions<HappyGopherOptions> options,
    GopherContentStore gopherContentStore,
    IMissionControlClient missionControlClient) : BackgroundService
{

    private TcpListener? _listener;
    private readonly ConcurrentDictionary<long, Task> _activeConnections = new();
    private volatile bool _stopRequested;
    private readonly SemaphoreSlim _connectionLimit = new(
        options.Value.MaxConcurrentConnections,
        options.Value.MaxConcurrentConnections);
    private long _nextConnectionId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IPAddress ipAddress = IPAddressUtils.ParseListenAddress(options.Value.ListenAddress);
        _listener = new TcpListener(ipAddress, options.Value.Port);
        _listener.Start();

        logger.LogInformation(
            "HappyGopher Server Listening on {address}:{port}; content root is {ContentRoot}",
            ipAddress,
            options.Value.Port,
            gopherContentStore.ContentRoot
        );

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (stoppingToken.IsCancellationRequested || _stopRequested)
                {
                    break;
                }
                try
                {
                    await _connectionLimit.WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    client.Dispose();
                    break;
                }

                long connectionId = Interlocked.Increment(ref _nextConnectionId);
                Task task = HandleClientAsync(connectionId, client, stoppingToken);
                _activeConnections[connectionId] = task;

                _ = task.ContinueWith(
                    completedTask =>
                    {
                        _activeConnections.TryRemove(connectionId, out _);
                        _connectionLimit.Release();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
            }
        }
        finally
        {
            _listener.Stop();
            Task[] remaining = _activeConnections.Values.ToArray();
            if (remaining.Length > 0)
            {
                try
                {
                    await Task.WhenAll(remaining);
                }
                catch
                {
                    // Normal Shutdown
                }
            }

            logger.LogInformation("HappyGopher server stopped.");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("HappyGopher Server Stopping...");
        _stopRequested = true;
        _listener?.Stop();

        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(
        long connectionId,
        TcpClient client,
        CancellationToken stoppingToken)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString("N");

        string? selector = null;
        GopherResponseKind? responseKind = null;
        bool responseCompleted = false;

        using (client)
        {
            client.NoDelay = true;
            EndPoint? remote = client.Client.RemoteEndPoint;

            bool isIgnoredTelemetrySource =
                IsIgnoredTelemetrySource(remote);

            try
            {
                await using NetworkStream stream = client.GetStream();
                string? request = await ReadSelectorLineAsync(stream, stoppingToken);

                if (request is null)
                {
                    return;
                }

                int tabIndex = request.IndexOf("\t");
                selector = tabIndex >= 0
                    ? request[..tabIndex]
                    : request;

                logger.LogDebug(
                    "Connection {ConnectionId} from {Remote} requested selector {Selector}",
                    connectionId,
                    remote,
                    selector);

                responseKind =
                    await gopherContentStore.WriteResponseAsync(
                        selector,
                        stream,
                        stoppingToken);

                await stream.FlushAsync(stoppingToken);
                responseCompleted = true;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "Connection {ConnectionId} from {Remote} timed out.",
                    connectionId,
                    remote);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(
                    exception,
                    "Rejected malformed request on connection {ConnectionId} from {Remote}.",
                    connectionId,
                    remote);
            }
            catch (IOException exception)
            {
                logger.LogDebug(
                    exception,
                    "Connection {ConnectionId} from {Remote} ended early.",
                    connectionId,
                    remote);
            }
            catch (SocketException exception)
            {
                logger.LogDebug(
                    exception,
                    "Socket error on connection {ConnectionId} from {Remote}.",
                    connectionId,
                    remote);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled error on connection {ConnectionId} from {Remote}.",
                    connectionId,
                    remote);
            }

            stopwatch.Stop();
            if (selector is null || responseKind is null)
            {
                return;
            }

            var succeeded =
                responseCompleted &&
                responseKind is not GopherResponseKind.InvalidSelector &&
                responseKind is not GopherResponseKind.NotFound;

            if (isIgnoredTelemetrySource)
            {
                logger.LogDebug(
                    "Skipping telemetry for monitoring request from {Remote}.",
                    remote);

                return;
            }

            await PublishSelectorServedTelemetryAsync(
                selector,
                responseKind.Value,
                remote,
                stopwatch.ElapsedMilliseconds,
                succeeded,
                occurredAt,
                correlationId,
                stoppingToken);
        }
    }

    private bool IsIgnoredTelemetrySource(
    EndPoint? remote)
    {
        string? remoteAddress =
            (remote as IPEndPoint)?
                .Address
                .MapToIPv4()
                .ToString();

        return
            !string.IsNullOrWhiteSpace(
                options.Value.TelemetryIgnoredRemoteAddress) &&
            string.Equals(
                remoteAddress,
                options.Value.TelemetryIgnoredRemoteAddress,
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task PublishSelectorServedTelemetryAsync(
        string selector,
        GopherResponseKind responseKind,
        EndPoint? remote,
        long durationMilliseconds,
        bool succeeded,
        DateTimeOffset occurredAt,
        string correlationId,
        CancellationToken stoppingToken)
    {
        try
        {
            await missionControlClient.TryPublishAsync(
                eventType: "happygopher.selector.served",
                payload: new SelectorServedEvent(
                    selector,
                    ToResponseType(responseKind),
                    remote?.ToString() ?? "unknown",
                    durationMilliseconds,
                    succeeded),
                occurredAt,
                correlationId,
                stoppingToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to publish telemetry for selector {Selector}.",
                selector);
        }
    }

    private static string ToResponseType(
        GopherResponseKind responseKind)
    {
        return responseKind switch
        {
            GopherResponseKind.Menu =>
                "menu",

            GopherResponseKind.Text =>
                "text",

            GopherResponseKind.Binary =>
                "binary",

            GopherResponseKind.NotFound =>
                "not-found",

            GopherResponseKind.InvalidSelector =>
                "invalid-selector",

            _ =>
                "unknown"
        };
    }

    private Task<string?> ReadSelectorLineAsync(
        NetworkStream stream,
        CancellationToken stoppingToken) =>
        GopherSelectorReader.ReadAsync(
            stream,
            options.Value.MaxSelectorBytes,
            options.Value.RequestTimeoutSeconds,
            stoppingToken);
}
