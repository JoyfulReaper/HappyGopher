/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace HappyGopher.Pages;

/// <summary>
/// Dependency-injection registration helpers for dynamic Gopher pages.
/// </summary>
public static class GopherPageServiceCollectionExtensions
{
    /// <summary>
    /// Registers automatically discoverable Gopher pages from the assembly
    /// containing <typeparamref name="TMarker"/>.
    /// </summary>
    public static IServiceCollection AddGopherPagesFromAssemblyContaining<TMarker>(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddGopherPagesFromAssemblies(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Registers concrete, public <see cref="IGopherPage"/> implementations
    /// marked with <see cref="AutoRegisterGopherPageAttribute"/>.
    /// </summary>
    public static IServiceCollection AddGopherPagesFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (Assembly assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            IEnumerable<TypeInfo> pageTypes = assembly.DefinedTypes
                .Where(IsAutoRegisteredPage)
                .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal);

            foreach (TypeInfo pageType in pageTypes)
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IGopherPage), pageType.AsType()));
            }
        }

        return services;
    }

    private static bool IsAutoRegisteredPage(TypeInfo type)
    {
        bool isPublic =
            type.IsPublic ||
            type.IsNestedPublic;

        return isPublic &&
            type.IsClass &&
            !type.IsAbstract &&
            !type.ContainsGenericParameters &&
            typeof(IGopherPage).IsAssignableFrom(type) &&
            type.IsDefined(typeof(AutoRegisterGopherPageAttribute), inherit: false);
    }
}