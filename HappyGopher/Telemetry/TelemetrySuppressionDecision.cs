/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Net;

namespace HappyGopher.Telemetry;

internal enum TelemetrySuppressionReason
{
    RemoteAddress,
    Selector,
    RemoteAddressAndSelector
}

internal readonly record struct TelemetrySuppressionDecision(
    bool IsSuppressed,
    TelemetrySuppressionReason? Reason)
{
    public static TelemetrySuppressionDecision NotSuppressed { get; } =
        new(false, null);

    public string? LogReason => Reason switch
    {
        TelemetrySuppressionReason.RemoteAddress => "remote-address",
        TelemetrySuppressionReason.Selector => "selector",
        TelemetrySuppressionReason.RemoteAddressAndSelector =>
            "remote-address-and-selector",
        _ => null
    };
}

internal static class TelemetrySuppression
{
    public static TelemetrySuppressionDecision Evaluate(
        EndPoint? remote,
        string selector,
        string? ignoredRemoteAddress,
        IEnumerable<string>? ignoredSelectors)
    {
        ArgumentNullException.ThrowIfNull(selector);

        bool addressConfigured =
            !string.IsNullOrWhiteSpace(ignoredRemoteAddress);
        bool addressMatches =
            addressConfigured &&
            IsRemoteAddressMatch(remote, ignoredRemoteAddress);

        string[] configuredSelectors = ignoredSelectors?
            .Where(configuredSelector =>
                !string.IsNullOrWhiteSpace(configuredSelector))
            .ToArray() ?? [];
        bool selectorsConfigured = configuredSelectors.Length > 0;
        bool selectorMatches =
            selectorsConfigured &&
            configuredSelectors.Contains(selector, StringComparer.Ordinal);

        if (addressConfigured && selectorsConfigured)
        {
            return addressMatches && selectorMatches
                ? new TelemetrySuppressionDecision(
                    true,
                    TelemetrySuppressionReason.RemoteAddressAndSelector)
                : TelemetrySuppressionDecision.NotSuppressed;
        }

        if (addressMatches)
        {
            return new TelemetrySuppressionDecision(
                true,
                TelemetrySuppressionReason.RemoteAddress);
        }

        return selectorMatches
            ? new TelemetrySuppressionDecision(
                true,
                TelemetrySuppressionReason.Selector)
            : TelemetrySuppressionDecision.NotSuppressed;
    }

    private static bool IsRemoteAddressMatch(
        EndPoint? remote,
        string? ignoredRemoteAddress)
    {
        if (remote is not IPEndPoint remoteEndPoint ||
            !IPAddress.TryParse(ignoredRemoteAddress, out IPAddress? ignoredAddress))
        {
            return false;
        }

        return NormalizeAddress(remoteEndPoint.Address).Equals(
            NormalizeAddress(ignoredAddress));
    }

    internal static IPAddress NormalizeAddress(IPAddress address) =>
        address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
}
