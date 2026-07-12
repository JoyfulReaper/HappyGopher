/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher;

internal sealed record SelectorServedEvent(
    string Selector,
    string ResponseType,
    long DurationMilliseconds,
    bool Succeeded);
