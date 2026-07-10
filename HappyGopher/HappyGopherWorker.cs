/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HappyGopher;

public class HappyGopherWorker(
    ILogger<HappyGopherWorker> logger,
    IOptions<HappyGopherOptions> options,
    GopherContentStore gopherContentStore) : BackgroundService
{

    private TcpListener? _listener;
    private readonly ConcurrentDictionary<long, Task> _activeConnections = new();
    private volatile bool _stopRequested;
    private readonly SemaphoreSlim _connectionLimit = new(
        options.Value.MaxConcurrentConnections,
        options.Value.MaxConcurrentConnections);
    private long _nextConnectionId;
    private static readonly Encoding SelectorEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IPAddress iPAddress = ParseListenAddress(options.Value.ListenAddress);
        _listener = new TcpListener(iPAddress, options.Value.Port);
        _listener.Start();

        logger.LogInformation(
            "HappyGopher Server Listening on {address}:{port}; content root is {ContentRoot}",
            iPAddress,
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
        _stopRequested = true;
        _listener?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(
        long connectionId,
        TcpClient client,
        CancellationToken stoppingToken)
    {
        using (client)
        {
            client.NoDelay = true;
            EndPoint? remote = client.Client.RemoteEndPoint;

            try
            {
                await using NetworkStream stream = client.GetStream();
                string? request = await ReadSelectorLineAsync(stream, stoppingToken);

                if (request is null)
                {
                    return;
                }

                int tabIndex = request.IndexOf("\t");
                string selector = tabIndex >= 0 ? request[..tabIndex] : request;

                logger.LogDebug(
                    "Connection {ConnectionId} from {Remote} requested selector {Selector}",
                    connectionId,
                    remote,
                    selector);

                await gopherContentStore.WriteResponseAsync(selector, stream, stoppingToken);
                await stream.FlushAsync(stoppingToken);
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
        }

    }

    private async Task<string?> ReadSelectorLineAsync(
        NetworkStream stream,
        CancellationToken stoppingToken)
    {
        int maxSelectorBytes = options.Value.MaxSelectorBytes;

        // Two extra bytes allow an exact-length selector followed by CRLF.
        int capacity = checked(maxSelectorBytes + 2);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        timeout.CancelAfter(
            TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));

        try
        {
            int count = 0;

            while (true)
            {
                int remainingCapacity = capacity - count;

                if (remainingCapacity == 0)
                {
                    throw new InvalidDataException(
                        $"Selector exceeded the {maxSelectorBytes} byte limit.");
                }

                int bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(count, remainingCapacity),
                    timeout.Token);

                if (bytesRead == 0)
                {
                    // Client disconnected without sending anything.
                    if (count == 0)
                    {
                        return null;
                    }

                    if (count > maxSelectorBytes)
                    {
                        throw new InvalidDataException(
                            $"Selector exceeded the {maxSelectorBytes} byte limit.");
                    }

                    return DecodeSelector(buffer, count);
                }

                ReadOnlySpan<byte> received =
                    buffer.AsSpan(count, bytesRead);

                int newlineOffset = received.IndexOf((byte)'\n');

                if (newlineOffset >= 0)
                {
                    int lineLength = count + newlineOffset;

                    // Strip the CR from a normal CRLF request.
                    if (lineLength > 0 &&
                        buffer[lineLength - 1] == (byte)'\r')
                    {
                        lineLength--;
                    }

                    if (lineLength > maxSelectorBytes)
                    {
                        throw new InvalidDataException(
                            $"Selector exceeded the {maxSelectorBytes} byte limit.");
                    }

                    return DecodeSelector(buffer, lineLength);
                }

                count += bytesRead;

                if (count > maxSelectorBytes)
                {
                    // An exact-length selector may still have a trailing CR
                    // while we wait for the final LF.
                    bool awaitingLfAfterCr =
                        count == maxSelectorBytes + 1 &&
                        buffer[count - 1] == (byte)'\r';

                    if (!awaitingLfAfterCr)
                    {
                        throw new InvalidDataException(
                            $"Selector exceeded the {maxSelectorBytes} byte limit.");
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string DecodeSelector(byte[] buffer, int length) =>
        SelectorEncoding.GetString(buffer, 0, length);

    private static IPAddress ParseListenAddress(string value)
    {
        if (value is "*" or "+" or "0.0.0.0")
        {
            return IPAddress.Any;
        }

        if (value == "::")
        {
            return IPAddress.IPv6Any;
        }

        if (!IPAddress.TryParse(value, out IPAddress? address))
        {
            throw new InvalidOperationException(
                $"HappyGopher: Invalid listen address: {value}."
            );
        }

        return address;
    }
}
