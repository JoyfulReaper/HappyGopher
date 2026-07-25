/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


namespace HappyGopher.Events;

public sealed record GopherServiceStartedEvent(string ListenAddress)
{
    public const string EventName = "happygopher.service.started";
}