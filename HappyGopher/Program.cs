/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher;
using HappyGopher.MissionControl;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Happy Gopher Server";
});


builder.Services
    .AddOptions<HappyGopherOptions>()
    .Bind(builder.Configuration.GetSection(HappyGopherOptions.SectionName))
    .Validate(options => options.Port is > 0 and <= 65535, "Gopher:Port must be between 1 and 65535.")
    .Validate(options => options.MaxConcurrentConnections > 0, "Gopher:MaxConcurrentConnections must be positive.")
    .Validate(options => options.MaxSelectorBytes is >= 64 and <= 65536, "Gopher:MaxSelectorBytes must be between 64 and 65536.")
    .Validate(options => options.RequestTimeoutSeconds > 0, "Gopher:RequestTimeoutSeconds must be positive.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ContentRoot), "Gopher:ContentRoot must not be empty.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.PublicHost), "Gopher:PublicHost must not be empty.")
    .ValidateOnStart();

builder.Services
    .AddOptions<MissionControlOptions>()
    .Bind(
        builder.Configuration.GetSection(
            MissionControlOptions.SectionName))
    .Validate(
        options =>
            !options.Enabled ||
            Uri.TryCreate(
                options.BaseUrl,
                UriKind.Absolute,
                out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps),
        "MissionControl:BaseUrl must be an absolute HTTP or HTTPS URL.")
    .Validate(
        options =>
            !options.Enabled ||
            options.ApiKey.Length >= 32,
        "MissionControl:ApiKey must contain at least 32 characters when enabled.")
    .Validate(
        options =>
            options.TimeoutMilliseconds is >= 100 and <= 10_000,
        "MissionControl:TimeoutMilliseconds must be between 100 and 10000.")
    .ValidateOnStart();

builder.Services.AddHttpClient(
    MissionControlOptions.HttpClientName,
    (services, client) =>
    {
        var options = services
            .GetRequiredService<IOptions<MissionControlOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl);

        client.Timeout = TimeSpan.FromMilliseconds(
            options.TimeoutMilliseconds);
    });

builder.Services.AddSingleton<
    IMissionControlClient,
    MissionControlClient>();

builder.Services.AddHostedService<HappyGopherWorker>();
builder.Services.AddSingleton<GopherContentStore>();

var host = builder.Build();
host.Run();
