/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HappyGopher.Events;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(GopherServiceStartedEvent))]
[JsonSerializable(typeof(SelectorServedEvent))]
internal sealed partial class HappyGopherJsonContext : JsonSerializerContext;