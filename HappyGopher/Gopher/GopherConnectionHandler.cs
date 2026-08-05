/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Pages;
using HappyGopher.Telemetry;
using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HappyGopher.Gopher;

public sealed class GopherConnectionHandler(
    ILogger<GopherConnectionHandler> logger,
    IOptions<HappyGopherOptions> options,
    GopherPageResolver gopherPageResolver,
    GopherContentStore gopherContentStore,
    TelemetryService telemetryService) : ITcpConnectionHandler
{
    public async ValueTask HandleAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken)
    {
        GopherSessionResult? result = await ProcessAsync(
            context.ConnectionId,
            context.Stream,
            context.RemoteEndPoint,
            cancellationToken);

        if (result is null)
        {
            return;
        }

        long connectionId = context.ConnectionId;

        context.RegisterAfterClose(afterCloseToken =>
            telemetryService.PublishSelectorServedTelemetryAsync(
                connectionId,
                result,
                afterCloseToken));
    }

    private async Task<GopherSessionResult?> ProcessAsync(
        long connectionId,
        Stream stream,
        EndPoint? remote,
        CancellationToken cancellationToken)
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string correlationId = Guid.NewGuid().ToString("N");

        string? selector = null;
        GopherResponseKind? responseKind = null;
        bool responseCompleted = false;

        bool isIgnoredTelemetrySource = TelemetryService.IsIgnoredTelemetrySource(remote, options.Value.TelemetryIgnoredRemoteAddress);

        try
        {
            GopherRequest? request = await GopherSelectorReader.ReadAsync(
                stream,
                options.Value.MaxSelectorBytes,
                options.Value.MaxInputBytes,
                options.Value.RequestTimeoutSeconds,
                cancellationToken);

            if (request is null)
            {
                return null;
            }

            selector = request.Selector;

            logger.LogDebug(
                "Connection {ConnectionId} from {Remote} requested selector {Selector}",
                connectionId,
                remote,
                selector);

            IGopherPage? page = gopherPageResolver.Resolve(selector);
            responseKind = page is null
                ? await gopherContentStore.WriteResponseAsync(
                    selector,
                    stream,
                    cancellationToken)
                : await page.WriteAsync(
                    request,
                    stream,
                    cancellationToken);
            responseCompleted = true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Connection {ConnectionId} from {Remote} was canceled during shutdown.",
                connectionId,
                remote);
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

}
