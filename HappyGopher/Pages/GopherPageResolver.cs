/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Extensibility;

namespace HappyGopher.Pages;

/// <summary>
/// Resolves registered dynamic Gopher pages by selector.
/// </summary>
public sealed class GopherPageResolver
{
    private readonly Dictionary<string, IGopherPage> _pages = new(StringComparer.Ordinal);
    private readonly List<(string Prefix, IGopherPage Page)> _subselectorPages = [];


    public GopherPageResolver(IEnumerable<IGopherPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        foreach (IGopherPage page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);

            string selector = NormalizeSelector(page.Selector);

            if (!_pages.TryAdd(selector, page))
            {
                throw new InvalidOperationException($"Multiple Gopher pages are registered for selector '{selector}'.");
            }

            if (page is IGopherSubselectorPage)
            {
                if (selector == "/" || selector.EndsWith('/'))
                {
                    throw new InvalidOperationException(
                        $"Invalid subselector page prefix '{selector}'.");
                }

                _subselectorPages.Add(($"{selector}/", page));
            }
        }

        _subselectorPages.Sort((left, right) =>
            right.Prefix.Length.CompareTo(left.Prefix.Length));
    }

    /// <summary>
    /// Resolves a dynamic page registered for the supplied selector.
    /// </summary>
    public IGopherPage? Resolve(string selector)
    {
        string normalizedSelector = NormalizeSelector(selector);

        if (_pages.TryGetValue(normalizedSelector, out IGopherPage? page))
        {
            return page;
        }

        foreach ((string prefix, IGopherPage subselectorPage) in _subselectorPages)
        {
            if (normalizedSelector.StartsWith(prefix, StringComparison.Ordinal))
            {
                return subselectorPage;
            }
        }

        return null;
    }

    private static string NormalizeSelector(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (selector.Length == 0)
        {
            return "/";
        }

        return selector[0] == '/'
            ? selector
            : "/" + selector;
    }
}