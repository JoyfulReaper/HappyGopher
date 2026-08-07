/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;
using HappyGopher.Events;
using HappyGopher.Gopher;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;
using System.Net;

namespace HappyGopher.Telemetry;

public sealed class TelemetryService(
    IMissionControlClient missionControlClient,
    IOptions<MissionControlClientOptions> missionControlOptions,
    ILogger<TelemetryService> logger)
{
    private static readonly TimeSpan TelemetryPublishTimeout = TimeSpan.FromSeconds(2); // TODO Make configurable

    internal async ValueTask PublishSelectorServedTelemetryAsync(
        long connectionId,
        GopherSessionResult result,
        CancellationToken cancellationToken)
    {

        if (!missionControlOptions.Value.Enabled)
        {
            return;
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TelemetryPublishTimeout);

        try
        {
            bool published = await missionControlClient.TryPublishAsync(
                eventType: SelectorServedEvent.EventName,
                payload: new SelectorServedEvent(
                    result.Selector,
                    ToResponseType(result.ResponseKind),
                    result.Remote,
                    result.DurationMilliseconds,
                    result.Succeeded),
                payloadTypeInfo: HappyGopherJsonContext.Default.SelectorServedEvent,
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

    internal static string FormatRemoteEndPoint(EndPoint? remote) =>
        remote is IPEndPoint remoteEndPoint
            ? new IPEndPoint(
                TelemetrySuppression.NormalizeAddress(remoteEndPoint.Address),
                remoteEndPoint.Port).ToString()
            : remote?.ToString() ?? "unknown";
}
