/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Tests;

public sealed class GopherMenuFieldsTests
{
    [Theory]
    [InlineData("tab\tvalue", "tab value")]
    [InlineData("carriage\rreturn", "carriage return")]
    [InlineData("new\nline", "new line")]
    public void Sanitize_ReplacesControlCharactersWithSpaces(
        string value,
        string expected)
    {
        Assert.Equal(expected, GopherMenuFields.Sanitize(value));
    }
}
