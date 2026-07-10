/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Options;
using System.Text;

namespace HappyGopher;

public sealed class GopherContentStore
{
    public GopherContentStore(
        IOptions<HappyGopherOptions> options,
        ILogger<GopherContentStore> logger)
    {
        _options = options.Value;
        _logger = logger;

        ContentRoot = Path.GetFullPath(
            Path.IsPathRooted(_options.ContentRoot)
                ? _options.ContentRoot
                : Path.Combine(AppContext.BaseDirectory, _options.ContentRoot));

        Directory.CreateDirectory(ContentRoot);
        _rootWithSeparator = ContentRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    private static readonly Encoding WireEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false);

    private readonly HappyGopherOptions _options;
    private readonly ILogger<GopherContentStore> _logger;
    private readonly string _rootWithSeparator;

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

    public async Task WriteResponseAsync(
        string selector,
        Stream output,
        CancellationToken cancellationToken)
    {
        string? path = ResolveSelector(selector);

        if (path is null)
        {
            await WriteErrorAsync(output, "Invalid Selector.", cancellationToken);
            return;
        }

        if (Directory.Exists(path))
        {
            await WriteDirectoryMenuAsync(path, output, cancellationToken);
            return;
        }

        if (!File.Exists(path) || IsInternalFile(path))
        {
            await WriteErrorAsync(output, "Selector not found.", cancellationToken);
            return;
        }

        if (IsTextFile(path))
        {
            await WriteTextFileAsync(path, output, cancellationToken);
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
        }
    }

    private static async Task WriteTextFileAsync(
        string path,
        Stream output,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(
            path,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true
        );

        await using var writer = CreateWriter(output);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith('.'))
                line = "." + line;

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await WriteTerminatorAsync(writer, cancellationToken);
    }

    private static bool IsTextFile(string path) =>
        TextExtensions.Contains(Path.GetExtension(path));

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

        await using var writer = CreateWriter(output);
        foreach (string childDir in directories)
        {
            string name = Path.GetFileName(childDir);
            string selector = PathToSelector(childDir);

            await WriteMenuItemAsync(
                writer,
                '1',
                name + "/",
                selector,
                _options.PublicHost,
                _options.Port,
                cancellationToken
            );
        }

        foreach (string file in files)
        {
            await WriteMenuItemAsync(
                writer,
                GetItemType(file),
                Path.GetFileName(file),
                PathToSelector(file),
                _options.PublicHost,
                _options.Port,
                cancellationToken);
        }

        await WriteTerminatorAsync(writer, cancellationToken);
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

        return TextExtensions.Contains(extension) ? '0' : '9';
    }

    private static int ComparePathsByName(string left, string right) =>
        StringComparer.OrdinalIgnoreCase.Compare(
            Path.GetFileName(left),
            Path.GetFileName(right));

    private static bool IsInternalFile(string path) =>
        string.Equals(
            Path.GetFileName(path),
            "gophermap",
            StringComparison.OrdinalIgnoreCase);

    private async Task WriteGopherMapAsync(
        string directory,
        string mapPath,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using var writer = CreateWriter(output);
        string directorySelector = PathToSelector(directory);

        using var reader = new StreamReader(mapPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (await reader.ReadLineAsync(cancellationToken) is { } rawLine)
        {
            if (rawLine.StartsWith('#'))
            {
                continue;
            }

            if (rawLine.Length == 0)
            {
                await WriteInfoAsync(writer, string.Empty, cancellationToken);
                continue;
            }

            char type = rawLine[0];
            if (!IsKnownMenuType(type))
            {
                await WriteInfoAsync(writer, rawLine, cancellationToken);
                continue;
            }

            string[] fields = rawLine[1..].Split('\t');
            string display = fields.ElementAtOrDefault(0) ?? string.Empty;

            if (type == 'i')
            {
                await WriteInfoAsync(writer, display, cancellationToken);
                continue;
            }

            string selector = fields.ElementAtOrDefault(1) ?? string.Empty;
            string? explicitHost = fields.ElementAtOrDefault(2);
            string? explicitPort = fields.ElementAtOrDefault(3);

            bool isLocalItem = string.IsNullOrWhiteSpace(explicitHost);
            string host = isLocalItem ? _options.PublicHost : explicitHost!;
            int port =
                int.TryParse(explicitPort, out int parsedPort) &&
                parsedPort is > 0 and <= 65535
                    ? parsedPort
                    : _options.Port;

            if (isLocalItem &&
                !selector.StartsWith('/') &&
                !selector.StartsWith("URL:", StringComparison.OrdinalIgnoreCase)
                )
            {
                selector = CombineSelectors(directorySelector, selector);
            }

            await WriteMenuItemAsync(
                writer,
                type,
                display,
                selector,
                host,
                port,
                cancellationToken);
        }
        await WriteTerminatorAsync(writer, cancellationToken);
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

    private static async Task WriteInfoAsync(
        StreamWriter writer,
        string display,
        CancellationToken cancellationToken
    )
    {
        await WriteMenuItemAsync(
            writer,
            'i',
            display,
            "fake",
            "(NULL)",
            0,
            cancellationToken);
    }

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
        await using var writer = CreateWriter(output);

        await WriteMenuItemAsync(
            writer,
            '3',
            message,
            "error",
            _options.PublicHost,
            _options.Port,
            cancellationToken);

        await WriteTerminatorAsync(writer, cancellationToken);
    }

    private static async Task WriteTerminatorAsync(
        StreamWriter writer,
        CancellationToken cancellationToken
    )
    {
        await writer.WriteLineAsync(".".AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task WriteMenuItemAsync(
        StreamWriter writer,
        char type,
        string display,
        string selector,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        string line = string.Concat(
            type,
            SanitizeField(display), "\t",
            SanitizeField(selector), "\t",
            SanitizeField(host), "\t",
            port.ToString(System.Globalization.CultureInfo.InvariantCulture));

        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    private static string SanitizeField(string value) =>
        value.Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');

    private static StreamWriter CreateWriter(Stream output) =>
        new(output, WireEncoding, bufferSize: 4096, leaveOpen: true)
        {
            NewLine = "\r\n"
        };

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
            candidate = Path.GetFullPath(Path.Combine(
                ContentRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }


        bool isRoot = string.Equals(
            candidate,
            ContentRoot,
            StringComparison.OrdinalIgnoreCase);

        bool isInsideRoot = candidate.StartsWith(
            _rootWithSeparator,
            StringComparison.OrdinalIgnoreCase);

        if (!isRoot && !isInsideRoot)
        {
            _logger.LogWarning("Blocked path traversal selector {Selector}", selector);
            return null;
        }

        if (ContainsReparsePoint(candidate))
        {
            _logger.LogWarning("Blocked selector through a reparse point: {Selector}", selector);
            return null;
        }

        return candidate;
    }

    private bool ContainsReparsePoint(string canidate)
    {
        string relative = Path.GetRelativePath(ContentRoot, canidate);
        if (relative == ".")
            return false;

        string current = ContentRoot;
        foreach (string part in relative.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                break;
            }

            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
        return false;
    }
}