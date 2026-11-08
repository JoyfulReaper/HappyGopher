/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGopher.Extensibility;

/// <summary>
/// Allows an external HappyGopher plugin to register its own services.
/// </summary>
public interface IHappyGopherPlugin
{
    void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration);
}