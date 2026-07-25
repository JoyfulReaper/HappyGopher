/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Events;

internal sealed record SelectorServedEvent(
    string Selector,
    string ResponseType,
    string Remote,
    long DurationMilliseconds,
    bool Succeeded);
