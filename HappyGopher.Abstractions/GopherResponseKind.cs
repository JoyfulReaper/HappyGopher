/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Abstractions;

public enum GopherResponseKind
{
    InvalidSelector,
    Menu,
    NotFound,
    Text,
    Binary
}