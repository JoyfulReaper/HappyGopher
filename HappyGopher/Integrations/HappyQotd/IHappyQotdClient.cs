/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Integrations.HappyQotd;

public interface IHappyQotdClient
{
    Task<HappyQotdQuote?> GetQuoteOfTheDayAsync(CancellationToken cancellationToken = default);
}