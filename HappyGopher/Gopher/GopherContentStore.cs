/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Options;
using System.Text;

namespace HappyGopher.Gopher;

public sealed class GopherContentStore
{
    public GopherContentStore(
        IOptions<HappyGopherOptions> options,
        ILogger<GopherContentStore> logger)
    {
        _options = options.Value;
        _logger = logger;

        ContentRoot = Path.GetFullPath(Path.IsPathRooted(_options.ContentRoot)
            ? _options.ContentRoot
            : Path.Combine(AppContext.BaseDirectory, _options.ContentRoot));
    }

    private readonly HappyGopherOptions _options;
    private readonly ILogger<GopherContentStore> _logger;
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".text", ".md", ".markdown", ".log", ".csv",
        ".json", ".xml", ".html", ".htm", ".css", ".js",
        ".cs", ".fs", ".vb", ".ps1", ".cmd", ".bat",
        ".ini", ".cfg", ".conf", ".yaml", ".yml"
    };

    private static readonly HashSet<string> ImageExtensions = new(
    StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".svg", ".ico"
    };

    public string ContentRoot { get; }

    public async Task<GopherResponseKind> WriteResponseAsync(
        string selector,
        Stream output,
        CancellationToken cancellationToken)
    {
        string? path = ResolveSelector(selector);

        if (path is null)
        {
            await WriteErrorAsync(output, "Invalid Selector.", cancellationToken);
            return GopherResponseKind.InvalidSelector;
        }

        if (Directory.Exists(path))
        {
            await WriteDirectoryMenuAsync(path, output, cancellationToken);
            return GopherResponseKind.Menu;
        }

        if (!File.Exists(path) || IsInternalFile(path))
        {
            await WriteErrorAsync(output, "Selector not found.", cancellationToken);
            return GopherResponseKind.NotFound;
        }

        if (IsTextFile(path))
        {
            await WriteTextFileAsync(path, output, cancellationToken);
            return GopherResponseKind.Text;
        }
        else
        {
            await using FileStream file = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 64 * 1024,
                useAsync: true
            );

            await file.CopyToAsync(output, 64 * 1024, cancellationToken);
            return GopherResponseKind.Binary;
        }
    }

    private static async Task WriteTextFileAsync(
        string path,
        Stream output,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            path,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        await using GopherResponseWriter writer = new(output);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            await writer.WriteTextLineAsync(line, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static bool IsTextFile(string path)
    {
        string extension = Path.GetExtension(path);

        if (TextExtensions.Contains(extension))
        {
            return true;
        }

        // Unknown named extensions still default to binary.
        if (!string.IsNullOrEmpty(extension))
        {
            return false;
        }

        // Extensionless files get a small content check.
        return LooksLikeTextFile(path);
    }

    private static bool LooksLikeTextFile(string path)
    {
        const int sampleSize = 4096;
        Span<byte> buffer = stackalloc byte[sampleSize];

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        int bytesRead = stream.Read(buffer);

        for (int i = 0; i < bytesRead; i++)
        {
            byte value = buffer[i];

            if (value == 0)
            {
                return false;
            }

            bool allowedControl = value is (byte)'\r' or (byte)'\n' or (byte)'\t';
            if (value < 0x20 && !allowedControl)
            {
                return false;
            }
        }

        return true;
    }

    private async Task WriteDirectoryMenuAsync(
        string directory,
        Stream output,
        CancellationToken cancellationToken
    )
    {
        string mapPath = Path.Combine(directory, "gophermap");
        if (File.Exists(mapPath) && !ContainsReparsePoint(mapPath))
        {
            await WriteGopherMapAsync(directory, mapPath, output, cancellationToken);
            return;
        }

        string[] directories = Directory.GetDirectories(directory)
            .Where(path => !ContainsReparsePoint(path))
            .ToArray();
        string[] files = Directory.GetFiles(directory)
            .Where(path => !IsInternalFile(path) && !ContainsReparsePoint(path))
            .ToArray();

        Array.Sort(directories, ComparePathsByName);
        Array.Sort(files, ComparePathsByName);

        await using GopherResponseWriter writer = new(output);
        foreach (string childDir in directories)
        {
            string name = Path.GetFileName(childDir);
            string selector = PathToSelector(childDir);

            await writer.WriteMenuItemAsync(
                '1',
                name + "/",
                selector,
                _options.PublicHost,
                _options.Port,
                cancellationToken);
        }

        foreach (string file in files)
        {
            await writer.WriteMenuItemAsync(
                GetItemType(file),
                Path.GetFileName(file),
                PathToSelector(file),
                _options.PublicHost,
                _options.Port,
                cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static char GetItemType(string path)
    {
        string extension = Path.GetExtension(path);

        if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
        {
            return 'g';
        }

        if (ImageExtensions.Contains(extension))
        {
            return 'I';
        }

        return IsTextFile(path) ? '0' : '9';
    }

    private static int ComparePathsByName(string left, string right) =>
        StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(left), Path.GetFileName(right));

    private static bool IsInternalFile(string path) =>
        string.Equals(Path.GetFileName(path), "gophermap", StringComparison.OrdinalIgnoreCase);

    private async Task WriteGopherMapAsync(
        string directory,
        string mapPath,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using GopherResponseWriter writer = new(output);
        string directorySelector = PathToSelector(directory);

        using var reader = new StreamReader(
            mapPath,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        while (await reader.ReadLineAsync(cancellationToken) is { } rawLine)
        {
            if (rawLine.StartsWith('#'))
            {
                continue;
            }

            if (rawLine.Length == 0)
            {
                await writer.WriteInfoAsync(string.Empty, cancellationToken);
                continue;
            }

            char type = rawLine[0];
            if (!IsKnownMenuType(type))
            {
                await writer.WriteInfoAsync(rawLine, cancellationToken);
                continue;
            }

            string[] fields = rawLine[1..].Split('\t');
            string display = fields.ElementAtOrDefault(0) ?? string.Empty;

            if (type == 'i')
            {
                await writer.WriteInfoAsync(display, cancellationToken);
                continue;
            }

            string selector = fields.ElementAtOrDefault(1) ?? string.Empty;
            string? explicitHost = fields.ElementAtOrDefault(2);
            string? explicitPort = fields.ElementAtOrDefault(3);
            bool isLocalItem = string.IsNullOrWhiteSpace(explicitHost);

            string host = isLocalItem
                ? _options.PublicHost
                : explicitHost!;

            int port = int.TryParse(explicitPort, out int parsedPort) &&
                parsedPort is > 0 and <= 65535
                    ? parsedPort
                    : _options.Port;

            if (isLocalItem &&
                !selector.StartsWith('/') &&
                !selector.StartsWith(
                    "URL:", StringComparison.OrdinalIgnoreCase))
            {
                selector = CombineSelectors(directorySelector, selector);
            }

            await writer.WriteMenuItemAsync(
                type,
                display,
                selector,
                host,
                port,
                cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static string CombineSelectors(string directorySelector, string child)
    {
        string prefix = directorySelector == "/"
            ? string.Empty
            : directorySelector.TrimEnd('/');

        return prefix + "/" + child.TrimStart('/');
    }

    private static bool IsKnownMenuType(char value) =>
        "0123456789+TgIhis".Contains(value);

    private string PathToSelector(string path)
    {
        string relative = Path.GetRelativePath(ContentRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');

        return relative == "." ? "/" : "/" + relative;
    }
    private async Task WriteErrorAsync(
        Stream output,
        string message,
        CancellationToken cancellationToken)
    {
        await using GopherResponseWriter writer = new(output);

        await writer.WriteErrorAsync(
            message,
            _options.PublicHost,
            _options.Port,
            cancellationToken);

        await writer.CompleteAsync(cancellationToken);
    }

    private string? ResolveSelector(string selector)
    {
        string relative = selector
            .Replace('\\', '/')
            .TrimStart('/');

        if (relative.Contains(':'))
            return null;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(ContentRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }


        if (!GopherPathSecurity.IsInsideRoot(ContentRoot, candidate))
        {
            _logger.LogWarning(
                "Blocked path traversal selector {Selector}",
                selector);

            return null;
        }

        if (GopherPathSecurity.ContainsReparsePoint(ContentRoot, candidate))
        {
            _logger.LogWarning("Blocked selector through a reparse point: {Selector}", selector);
            return null;
        }

        return candidate;
    }

    private bool ContainsReparsePoint(string candidate) =>
        GopherPathSecurity.ContainsReparsePoint(ContentRoot, candidate);
}
