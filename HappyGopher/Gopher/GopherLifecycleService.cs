/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Events;
using JoyfulReaperLib.JRNet;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;
using System.Net;

namespace HappyGopher.Gopher;

public sealed class GopherLifecycleService(
    ILogger<GopherLifecycleService> logger,
    IMissionControlClient missionControlClient,
    IOptions<HappyGopherOptions> options,
    GopherContentStore gopherContentStore) : IHostedLifecycleService
{
    private static readonly TimeSpan TelemetryPublishTimeout =
        TimeSpan.FromSeconds(2); // TODO Make configurable.

    public Task StartingAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task StartedAsync(
        CancellationToken cancellationToken)
    {
        var listenAddress = IPAddressUtils.ParseListenAddress(options.Value.ListenAddress);
        string listenEndPoint = new IPEndPoint(
            listenAddress,
            options.Value.Port).ToString();

        logger.LogInformation(
            "HappyGopher Server Listening on {ListenEndPoint}; dual mode is {DualMode}; content root is {ContentRoot}",
            listenEndPoint,
            options.Value.DualMode,
            gopherContentStore.ContentRoot);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TelemetryPublishTimeout);

        try
        {
            bool published = await missionControlClient.TryPublishAsync(
                eventType: GopherServiceStartedEvent.EventName,
                payload: new GopherServiceStartedEvent(listenEndPoint),
                payloadTypeInfo: HappyGopherJsonContext.Default.GopherServiceStartedEvent,
                occurredAt: DateTimeOffset.UtcNow,
                correlationId: null,
                cancellationToken: timeout.Token);

            if (!published)
            {
                logger.LogWarning(
                    "Mission Control did not accept {EventType}.",
                    GopherServiceStartedEvent.EventName);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("HappyGopher startup telemetry publishing was canceled.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Timed out publishing HappyGopher startup telemetry.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to publish HappyGopher startup telemetry.");
        }
    }

    public Task StoppingAsync(
        CancellationToken cancellationToken)
    {
        logger.LogInformation("HappyGopher Server Stopping...");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("HappyGopher server stopped.");

        return Task.CompletedTask;
    }
}
