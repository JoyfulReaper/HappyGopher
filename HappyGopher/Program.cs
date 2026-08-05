/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;
using HappyGopher.Integrations.HappyQotd;
using HappyGopher.Pages;
using HappyGopher.Pages.Guestbook;
using HappyGopher.Telemetry;
using JoyfulReaperLib.MissionControl;
using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// TODO This file needs to be refactored into a proper Startup class

// Windows Service Support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Happy Gopher Service";
});

// Gopher Configuration
builder.Services
    .AddOptions<HappyGopherOptions>()
    .Bind(builder.Configuration.GetSection(HappyGopherOptions.SectionName))
    .Validate(options => options.Port is > 0 and <= 65535, "Gopher:Port must be between 1 and 65535.")
    .Validate(options => options.MaxConcurrentConnections > 0, "Gopher:MaxConcurrentConnections must be positive.")
    .Validate(options => options.MaxSelectorBytes is >= 64 and <= 65536, "Gopher:MaxSelectorBytes must be between 64 and 65536.")
    .Validate(options => options.MaxInputBytes is >= 1 and <= 65536, "Gopher:MaxInputBytes must be between 1 and 65536.")
    .Validate(options => options.RequestTimeoutSeconds > 0, "Gopher:RequestTimeoutSeconds must be positive.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ContentRoot), "Gopher:ContentRoot must not be empty.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.PublicHost), "Gopher:PublicHost must not be empty.")
    .ValidateOnStart();

// Mission Control Integration
builder.Services.AddMissionControlClient(
    builder.Configuration.GetSection(
        MissionControlClientOptions.SectionName));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<GopherContentStore>();
builder.Services.AddSingleton<TelemetryService>();
builder.Services.AddScoped<GopherPageResolver>();
builder.Services.AddTcpServer<GopherConnectionHandler, HappyGopherOptions>();
builder.Services.AddHostedService<GopherLifecycleService>();

// Currently all pages must be registered here.
builder.Services.AddScoped<IGopherPage, ServerTimePage>();

// QOTD integration
if (builder.Configuration.GetValue<bool>($"{HappyQotdOptions.SectionName}:Enabled"))
{
    builder.Services.AddOptions<HappyQotdOptions>()
    .Bind(builder.Configuration.GetSection(HappyQotdOptions.SectionName))
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "HappyQotd:BaseUrl must be an absolute URI when the integration is enabled.")
    .Validate(options =>
        options.TimeoutMilliseconds > 0,
        "HappyQotd:TimeoutMilliseconds must be positive when the integration is enabled.")
    .ValidateOnStart();

    builder.Services.AddHttpClient<IHappyQotdClient, HappyQotdClient>(
       (services, client) =>
       {
           HappyQotdOptions options = services
               .GetRequiredService<IOptions<HappyQotdOptions>>()
               .Value;

           client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
           client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
       });
    builder.Services.AddScoped<IGopherPage, QuoteOfTheDayPage>();
    builder.Services.AddScoped<IGopherPage, RandomQuotePage>();
}

// Guestbook integration
GuestbookServiceCollectionExtensions.AddGuestbookPages(builder.Services, builder.Configuration);

var host = builder.Build();
host.Run();
