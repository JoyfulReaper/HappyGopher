/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Abstractions;
using HappyGopher.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGopher.Tests;

public sealed class GopherPageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGopherPagesFromAssemblyContaining_RegistersMarkedPage()
    {
        ServiceCollection services = new();

        services.AddGopherPagesFromAssemblyContaining<
            GopherPageServiceCollectionExtensionsTests>();

        ServiceDescriptor registration = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IGopherPage) &&
                descriptor.ImplementationType == typeof(DiscoveredPage));

        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }

    [Fact]
    public void AddGopherPagesFromAssemblyContaining_IgnoresUnmarkedPage()
    {
        ServiceCollection services = new();

        services.AddGopherPagesFromAssemblyContaining<
            GopherPageServiceCollectionExtensionsTests>();

        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IGopherPage) &&
                descriptor.ImplementationType == typeof(UnmarkedPage));
    }

    [Fact]
    public void AddGopherPagesFromAssemblyContaining_IsIdempotent()
    {
        ServiceCollection services = new();

        services.AddGopherPagesFromAssemblyContaining<
            GopherPageServiceCollectionExtensionsTests>();

        services.AddGopherPagesFromAssemblyContaining<
            GopherPageServiceCollectionExtensionsTests>();

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IGopherPage) &&
                descriptor.ImplementationType == typeof(DiscoveredPage));
    }

    [Fact]
    public void AddGopherPagesFromAssemblyContaining_DoesNotRegisterOptionalPages()
    {
        ServiceCollection services = new();

        services.AddGopherPagesFromAssemblyContaining<HealthPage>();

        Type[] implementationTypes = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IGopherPage))
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .ToArray();

        Assert.Contains(typeof(HealthPage), implementationTypes);
        Assert.Contains(typeof(ServerTimePage), implementationTypes);

        Assert.DoesNotContain(
            typeof(RandomQuotePage),
            implementationTypes);

        Assert.DoesNotContain(
            typeof(QuoteOfTheDayPage),
            implementationTypes);
    }

    [AutoRegisterGopherPage]
    public sealed class DiscoveredPage : IGopherPage
    {
        public string Selector => "/discovered";

        public Task<GopherResponseKind> WriteAsync(
            GopherRequest request,
            Stream output,
            CancellationToken cancellationToken) =>
            Task.FromResult(GopherResponseKind.Text);
    }

    public sealed class UnmarkedPage : IGopherPage
    {
        public string Selector => "/unmarked";

        public Task<GopherResponseKind> WriteAsync(
            GopherRequest request,
            Stream output,
            CancellationToken cancellationToken) =>
            Task.FromResult(GopherResponseKind.Text);
    }
}