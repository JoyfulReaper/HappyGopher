/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRNet;
using Microsoft.Extensions.Options;

namespace HappyGopher;

public sealed class GopherLifecycleService(
    ILogger<GopherLifecycleService> logger,
    IOptions<HappyGopherOptions> options,
    GopherContentStore gopherContentStore) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        var listenAddress =
            IPAddressUtils.ParseListenAddress(
                options.Value.ListenAddress);

        logger.LogInformation(
            "HappyGopher Server Listening on {Address}:{Port}; content root is {ContentRoot}",
            listenAddress,
            options.Value.Port,
            gopherContentStore.ContentRoot);

        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
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