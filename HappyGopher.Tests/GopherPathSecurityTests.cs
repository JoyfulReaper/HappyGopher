/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Runtime.InteropServices;

namespace HappyGopher.Tests;

public sealed class GopherPathSecurityTests
{
    [Fact]
    public void IsInsideRoot_AcceptsContentRootItself()
    {
        using TestContentStore test = new();

        Assert.True(GopherPathSecurity.IsInsideRoot(test.Root, test.Root));
    }

    [Fact]
    public void IsInsideRoot_AcceptsChildFile()
    {
        using TestContentStore test = new();
        string candidate = Path.Combine(test.Root, "file.txt");

        Assert.True(GopherPathSecurity.IsInsideRoot(test.Root, candidate));
    }

    [Fact]
    public void IsInsideRoot_AcceptsNestedChildPath()
    {
        using TestContentStore test = new();
        string candidate = Path.Combine(test.Root, "one", "two", "file.txt");

        Assert.True(GopherPathSecurity.IsInsideRoot(test.Root, candidate));
    }

    [Fact]
    public void IsInsideRoot_AcceptsNormalizedPathThatRemainsInsideRoot()
    {
        using TestContentStore test = new();
        string candidate = Path.GetFullPath(Path.Combine(
            test.Root,
            "one",
            "..",
            "two",
            "file.txt"));

        Assert.True(GopherPathSecurity.IsInsideRoot(test.Root, candidate));
    }

    [Fact]
    public void IsInsideRoot_RejectsParentPath()
    {
        using TestContentStore test = new();
        string candidate = Path.GetFullPath(Path.Combine(test.Root, ".."));

        Assert.False(GopherPathSecurity.IsInsideRoot(test.Root, candidate));
    }

    [Fact]
    public void IsInsideRoot_RejectsSiblingDirectoryWithSimilarPrefix()
    {
        string parent = Path.Combine(
            Path.GetTempPath(),
            "HappyGopher.Tests",
            Guid.NewGuid().ToString("N"));
        string contentRoot = Path.Combine(parent, "content");
        string sibling = Path.Combine(parent, "content-secret", "file.txt");

        try
        {
            Directory.CreateDirectory(contentRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);

            Assert.False(GopherPathSecurity.IsInsideRoot(contentRoot, sibling));
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    [Fact]
    public void ContainsReparsePoint_DetectsDirectorySymbolicLinkWhenSupported()
    {
        using TestContentStore test = new();
        string target = Path.Combine(test.Root, "target");
        string link = Path.Combine(test.Root, "link");
        Directory.CreateDirectory(target);

        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            PlatformNotSupportedException)
        {
            return;
        }

        Assert.True(GopherPathSecurity.ContainsReparsePoint(test.Root, link));
    }

    [Fact]
    public async Task WriteResponseAsync_RejectsSelectorThroughDirectorySymbolicLinkWhenSupported()
    {
        using TestContentStore test = new();
        string target = Path.Combine(test.Root, "target");
        string link = Path.Combine(test.Root, "link");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "secret.txt"), "secret");

        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            PlatformNotSupportedException)
        {
            return;
        }

        string response = await test.GetResponseAsync("/link/secret.txt");

        Assert.Contains("3Invalid Selector.\terror\tgopher.test\t7070\r\n", response);
        Assert.DoesNotContain("secret", response);
    }
}
