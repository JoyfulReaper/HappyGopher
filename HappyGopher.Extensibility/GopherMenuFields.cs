/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Extensibility;

internal static class GopherMenuFields
{
    public static string Sanitize(string value) =>
        value.Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}
