/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Pages.Guestbook;

public static class GuestbookServiceCollectionExtensions
{
    public static IServiceCollection AddGuestbookPages(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        bool enabled = configuration.GetValue<bool>(
            $"{GuestbookOptions.SectionName}:Enabled");

        if (!enabled)
        {
            return services;
        }

        services
            .AddOptions<GuestbookOptions>()
            .Bind(configuration.GetSection(
                GuestbookOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IGuestbookStore, FileGuestbookStore>();
        services.AddScoped<IGopherPage, ViewGuestbookPage>();
        services.AddScoped<IGopherPage, SignGuestbookPage>();

        return services;
    }
}