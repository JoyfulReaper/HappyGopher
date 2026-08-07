/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;

namespace HappyGopher.Gopher;

internal sealed record GopherSessionResult(
    string Selector,
    GopherResponseKind ResponseKind,
    string Remote,
    long DurationMilliseconds,
    bool Succeeded,
    DateTimeOffset OccurredAt,
    string CorrelationId);