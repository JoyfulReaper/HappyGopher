/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Reflection;

namespace HappyGopher.Plugins;

/// <summary>
/// Represents a discovered plugin whose entry assembly has been loaded.
/// </summary>
public sealed record GopherLoadedPlugin(
    GopherPluginDescriptor Descriptor,
    Assembly EntryAssembly);