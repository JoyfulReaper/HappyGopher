/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Gopher;

public enum GopherResponseKind
{
    InvalidSelector,
    Menu,
    NotFound,
    Text,
    Binary
}