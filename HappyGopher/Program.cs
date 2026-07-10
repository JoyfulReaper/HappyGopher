/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher;

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
    .ValidateOnStart();

builder.Services.AddHostedService<HappyGopherWorker>();
builder.Services.AddSingleton<GopherContentStore>();

var host = builder.Build();
host.Run();
