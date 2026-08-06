/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Integrations.HappyQotd;

public sealed class HappyQotdOptions
{
    public const string SectionName = "HappyQotd";
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public int TimeoutMilliseconds { get; set; } = 2000;
    public bool Enabled { get; set; } = false;
}