/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher;

internal static class GopherMenuFields
{
    public static string Sanitize(string value) =>
        value.Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}
