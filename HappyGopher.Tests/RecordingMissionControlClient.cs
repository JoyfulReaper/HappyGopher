/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.MissionControl;
using System.Collections.Concurrent;

namespace HappyGopher.Tests;

public sealed record RecordedMissionControlEvent(
    string EventType,
    object? Payload,
    DateTimeOffset OccurredAt,
    string? CorrelationId);

public sealed class RecordingMissionControlClient : IMissionControlClient
{
    private readonly ConcurrentQueue<RecordedMissionControlEvent> _events = new();
    private readonly SemaphoreSlim _eventSignal = new(0);
    private int _publishedEventCount;

    public IReadOnlyList<RecordedMissionControlEvent> PublishedEvents => _events.ToArray();

    public Task<bool> TryPublishAsync<TPayload>(
        string eventType,
        TPayload payload,
        DateTimeOffset occurredAt,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        _events.Enqueue(new RecordedMissionControlEvent(
            eventType,
            payload,
            occurredAt,
            correlationId));

        Interlocked.Increment(ref _publishedEventCount);
        _eventSignal.Release();

        return Task.FromResult(true);
    }

    public async Task WaitForPublishedEventCountAsync(
        int expectedCount,
        CancellationToken cancellationToken = default)
    {
        if (expectedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount));
        }

        while (Volatile.Read(ref _publishedEventCount) < expectedCount)
        {
            await _eventSignal.WaitAsync(cancellationToken);
        }
    }
}
