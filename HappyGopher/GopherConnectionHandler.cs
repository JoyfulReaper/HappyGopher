/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Events;
using JoyfulReaperLib.MissionControl;
using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HappyGopher;

public sealed class GopherConnectionHandler(
    ILogger<GopherConnectionHandler> logger,
    IOptions<HappyGopherOptions> options,
    GopherContentStore gopherContentStore,
    IMissionControlClient missionControlClient) : ITcpConnectionHandler
{
    private static readonly TimeSpan TelemetryPublishTimeout =
        TimeSpan.FromSeconds(2);

    public async ValueTask HandleAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken)
    {
        GopherSessionResult? result = await ProcessAsync(
            context.ConnectionId,
            context.Stream,
            context.RemoteEndPoint,
            gopherContentStore,
            options.Value,
            logger,
            cancellationToken);

        if (result is null)
        {
            return;
        }

        long connectionId = context.ConnectionId;

        context.RegisterAfterClose(afterCloseToken =>
            PublishSelectorServedTelemetryAsync(
                connectionId,
                result,
                missionControlClient,
                logger,
                afterCloseToken));
    }

    internal static async Task<GopherSessionResult?> ProcessAsync(
        long connectionId,
        Stream stream,
        EndPoint? remote,
        GopherContentStore gopherContentStore,
        HappyGopherOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string correlationId = Guid.NewGuid().ToString("N");

        string? selector = null;
        GopherResponseKind? responseKind = null;
        bool responseCompleted = false;

        bool isIgnoredTelemetrySource = IsIgnoredTelemetrySource(
            remote,
            options.TelemetryIgnoredRemoteAddress);

        try
        {
            string? request = await GopherSelectorReader.ReadAsync(
                stream,
                options.MaxSelectorBytes,
                options.RequestTimeoutSeconds,
                cancellationToken);

            if (request is null)
            {
                return null;
            }

            int tabIndex = request.IndexOf('\t');

            selector = tabIndex >= 0
                ? request[..tabIndex]
                : request;

            logger.LogDebug(
                "Connection {ConnectionId} from {Remote} requested selector {Selector}",
                connectionId,
                remote,
                selector);

            responseKind = await gopherContentStore.WriteResponseAsync(
                selector,
                stream,
                cancellationToken);

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
        finally
        {
            stopwatch.Stop();
        }

        if (selector is null || responseKind is null)
        {
            return null;
        }

        if (isIgnoredTelemetrySource)
        {
            logger.LogDebug(
                "Skipping telemetry for monitoring request from {Remote}.",
                remote);

            return null;
        }

        bool succeeded =
            responseCompleted &&
            responseKind is not GopherResponseKind.InvalidSelector &&
            responseKind is not GopherResponseKind.NotFound;

        return new GopherSessionResult(
            Selector: selector,
            ResponseKind: responseKind.Value,
            Remote: remote?.ToString() ?? "unknown",
            DurationMilliseconds: stopwatch.ElapsedMilliseconds,
            Succeeded: succeeded,
            OccurredAt: occurredAt,
            CorrelationId: correlationId);
    }

    internal static async ValueTask PublishSelectorServedTelemetryAsync(
        long connectionId,
        GopherSessionResult result,
        IMissionControlClient missionControlClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TelemetryPublishTimeout);

        try
        {
            bool published = await missionControlClient.TryPublishAsync(
                eventType: "happygopher.selector.served",
                payload: new SelectorServedEvent(
                    result.Selector,
                    ToResponseType(result.ResponseKind),
                    result.Remote,
                    result.DurationMilliseconds,
                    result.Succeeded),
                occurredAt: result.OccurredAt,
                correlationId: result.CorrelationId,
                cancellationToken: timeout.Token);

            if (!published)
            {
                logger.LogWarning(
                    "Mission Control did not accept telemetry for selector {Selector} on connection {ConnectionId}.",
                    result.Selector,
                    connectionId);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Telemetry publishing stopped for selector {Selector} on connection {ConnectionId}.",
                result.Selector,
                connectionId);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Timed out publishing telemetry for selector {Selector} on connection {ConnectionId}.",
                result.Selector,
                connectionId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to publish telemetry for selector {Selector} on connection {ConnectionId}.",
                result.Selector,
                connectionId);
        }
    }

    private static bool IsIgnoredTelemetrySource(
        EndPoint? remote,
        string? ignoredRemoteAddress)
    {
        string? remoteAddress =
            (remote as IPEndPoint)?
                .Address
                .MapToIPv4()
                .ToString();

        return
            !string.IsNullOrWhiteSpace(ignoredRemoteAddress) &&
            string.Equals(
                remoteAddress,
                ignoredRemoteAddress,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string ToResponseType(
        GopherResponseKind responseKind)
    {
        return responseKind switch
        {
            GopherResponseKind.Menu => "menu",
            GopherResponseKind.Text => "text",
            GopherResponseKind.Binary => "binary",
            GopherResponseKind.NotFound => "not-found",
            GopherResponseKind.InvalidSelector => "invalid-selector",
            _ => "unknown"
        };
    }
}

internal sealed record GopherSessionResult(
    string Selector,
    GopherResponseKind ResponseKind,
    string Remote,
    long DurationMilliseconds,
    bool Succeeded,
    DateTimeOffset OccurredAt,
    string CorrelationId);