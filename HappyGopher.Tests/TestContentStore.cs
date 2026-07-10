/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;

namespace HappyGopher.Tests;

public sealed class TestContentStore : IDisposable
{
    public TestContentStore()
    {
        Root = Path.Combine(Path.GetTempPath(), "HappyGopher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public GopherContentStore CreateStore(
        int port = 7070,
        string publicHost = "gopher.test") =>
        new(
            Options.Create(new HappyGopherOptions
            {
                ContentRoot = Root,
                Port = port,
                PublicHost = publicHost
            }),
            NullLogger<GopherContentStore>.Instance);

    public async Task<string> GetResponseAsync(string selector)
    {
        await using MemoryStream output = new();
        await CreateStore().WriteResponseAsync(selector, output, CancellationToken.None);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    public void WriteText(string relativePath, string contents)
    {
        string path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    public void WriteBytes(string relativePath, byte[] contents)
    {
        string path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
