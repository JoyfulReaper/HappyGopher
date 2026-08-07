/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HappyGopher.Pages.Guestbook;

public static class GuestbookServiceCollectionExtensions
{
    public static IServiceCollection AddGuestbookPages(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GuestbookOptions>()
            .Bind(configuration.GetSection(GuestbookOptions.SectionName))
            .Validate(options => options.MaxEntriesDisplayed > 0,
                "Guestbook:MaxEntriesDisplayed must be positive.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DataPath),
                "Guestbook:DataPath must not be empty.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        var guestbookEnabled = configuration.GetValue<bool>($"{GuestbookOptions.SectionName}:Enabled");
        if (guestbookEnabled)
        {
            services.AddSingleton<IGuestbookStore, FileGuestbookStore>();
            services.AddScoped<IGopherPage, ViewGuestbookPage>();
            services.AddScoped<IGopherPage, SignGuestbookPage>();
        }

        return services;
    }
}
