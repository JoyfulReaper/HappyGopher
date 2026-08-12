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
        }
    }

    /// <summary>
    /// Resolves a dynamic page registered for the supplied selector.
    /// </summary>
    public IGopherPage? Resolve(string selector)
    {
        string normalizedSelector = NormalizeSelector(selector);

        return _pages.TryGetValue(normalizedSelector, out IGopherPage? page)
            ? page
            : null;
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