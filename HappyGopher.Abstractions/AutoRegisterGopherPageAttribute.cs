/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Abstractions;

/// <summary>
/// Marks an <see cref="IGopherPage"/> implementation for automatic
/// dependency-injection registration.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class AutoRegisterGopherPageAttribute : Attribute
{
}