/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher;

internal static class GopherPathSecurity
{
    public static bool IsInsideRoot(string contentRoot, string candidate)
    {
        string relativePath = Path.GetRelativePath(
            contentRoot,
            candidate);

        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        if (relativePath == "..")
        {
            return false;
        }

        if (relativePath.StartsWith(
            $"..{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal))
        {
            return false;
        }

        return Path.AltDirectorySeparatorChar ==
               Path.DirectorySeparatorChar ||
               !relativePath.StartsWith(
                   $"..{Path.AltDirectorySeparatorChar}",
                   StringComparison.Ordinal);
    }

    public static bool ContainsReparsePoint(string contentRoot, string candidate)
    {
        string relative = Path.GetRelativePath(contentRoot, candidate);
        if (relative == ".")
        {
            return false;
        }

        string current = contentRoot;
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
