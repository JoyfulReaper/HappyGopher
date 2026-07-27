/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Net;
using System.Net.Http.Json;

namespace HappyGopher.Integrations.HappyQotd;

public sealed class HappyQotdClient(HttpClient httpClient) : IHappyQotdClient
{
    public async Task<HappyQotdQuote?> GetQuoteOfTheDayAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response =
            await httpClient.GetAsync("api/quotes/today", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<HappyQotdQuote>(cancellationToken);
    }
}