/*
 * Happy Gopher Server
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Telemetry;
using System.Net;

namespace HappyGopher.Tests;

public sealed class TelemetryServiceTests
{
    [Fact]
    public void Suppression_NativeIPv4MatchesConfiguredIPv4()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Parse("192.0.2.10"), 12345),
            ignoredAddress: "192.0.2.10");

        AssertSuppressed(decision, TelemetrySuppressionReason.RemoteAddress);
    }

    [Fact]
    public void Suppression_MappedIPv4MatchesConfiguredIPv4()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Parse("::ffff:192.0.2.10"), 12345),
            ignoredAddress: "192.0.2.10");

        AssertSuppressed(decision, TelemetrySuppressionReason.RemoteAddress);
    }

    [Fact]
    public void Suppression_NativeIPv6MatchesConfiguredIPv6()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Parse("2001:db8::10"), 12345),
            ignoredAddress: "2001:db8::10");

        AssertSuppressed(decision, TelemetrySuppressionReason.RemoteAddress);
    }

    [Fact]
    public void Suppression_DifferentIPv6DoesNotMatch()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Parse("2001:db8::10"), 12345),
            ignoredAddress: "2001:db8::11");

        AssertNotSuppressed(decision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public void Suppression_InvalidConfiguredAddressDoesNotMatch(
        string? ignoredAddress)
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Loopback, 12345),
            ignoredAddress: ignoredAddress);

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void Suppression_ExactSelectorMatchIsSuppressed()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            selector: "/healthz",
            ignoredSelectors: ["/healthz"]);

        AssertSuppressed(decision, TelemetrySuppressionReason.Selector);
    }

    [Fact]
    public void Suppression_SelectorMatchingIsCaseSensitive()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            selector: "/Healthz",
            ignoredSelectors: ["/healthz"]);

        AssertNotSuppressed(decision);
    }

    [Theory]
    [InlineData("/healthz/details")]
    [InlineData("/health")]
    public void Suppression_SelectorPrefixesDoNotMatch(string selector)
    {
        TelemetrySuppressionDecision decision = Evaluate(
            selector: selector,
            ignoredSelectors: ["/healthz"]);

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void Suppression_BlankSelectorEntriesAreIgnored()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            selector: "/healthz",
            ignoredSelectors: [null!, "", "   "]);

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void Suppression_OnlyAddressConfigured_MatchingAddressIsSuppressed()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Loopback, 12345),
            selector: "/about.txt",
            ignoredAddress: "127.0.0.1");

        AssertSuppressed(decision, TelemetrySuppressionReason.RemoteAddress);
    }

    [Fact]
    public void Suppression_OnlySelectorConfigured_MatchingSelectorIsSuppressed()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(IPAddress.Parse("198.51.100.20"), 12345),
            selector: "/healthz",
            ignoredSelectors: ["/healthz"]);

        AssertSuppressed(decision, TelemetrySuppressionReason.Selector);
    }

    [Fact]
    public void Suppression_BothConfigured_AddressAndSelectorMatch()
    {
        TelemetrySuppressionDecision decision = EvaluateCombined(
            IPAddress.Loopback,
            "/healthz");

        AssertSuppressed(
            decision,
            TelemetrySuppressionReason.RemoteAddressAndSelector);
    }

    [Fact]
    public void Suppression_BothConfigured_AddressMatchesButSelectorDiffers()
    {
        TelemetrySuppressionDecision decision = EvaluateCombined(
            IPAddress.Loopback,
            "/about.txt");

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void Suppression_BothConfigured_SelectorMatchesButAddressDiffers()
    {
        TelemetrySuppressionDecision decision = EvaluateCombined(
            IPAddress.Parse("198.51.100.20"),
            "/healthz");

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void Suppression_BothConfigured_NeitherMatches()
    {
        TelemetrySuppressionDecision decision = EvaluateCombined(
            IPAddress.Parse("198.51.100.20"),
            "/about.txt");

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void Suppression_ProductionProxyRegression_NormalSelectorPublishes()
    {
        TelemetrySuppressionDecision decision = Evaluate(
            new IPEndPoint(
                IPAddress.Parse("::ffff:172.21.0.1"),
                12345),
            selector: "/about.txt",
            ignoredAddress: "172.21.0.1",
            ignoredSelectors: ["/healthz"]);

        AssertNotSuppressed(decision);
    }

    [Fact]
    public void FormatRemoteEndPoint_FormatsNativeIPv4()
    {
        IPEndPoint remote = new(IPAddress.Parse("192.0.2.10"), 12345);

        Assert.Equal(
            "192.0.2.10:12345",
            TelemetryService.FormatRemoteEndPoint(remote));
    }

    [Fact]
    public void FormatRemoteEndPoint_NormalizesMappedIPv4()
    {
        IPEndPoint remote = new(
            IPAddress.Parse("::ffff:192.0.2.10"),
            12345);

        Assert.Equal(
            "192.0.2.10:12345",
            TelemetryService.FormatRemoteEndPoint(remote));
    }

    [Fact]
    public void FormatRemoteEndPoint_FormatsNativeIPv6WithBrackets()
    {
        IPEndPoint remote = new(IPAddress.Parse("2001:db8::10"), 12345);

        Assert.Equal(
            "[2001:db8::10]:12345",
            TelemetryService.FormatRemoteEndPoint(remote));
    }

    private static TelemetrySuppressionDecision Evaluate(
        EndPoint? remote = null,
        string selector = "/selector",
        string? ignoredAddress = null,
        IEnumerable<string>? ignoredSelectors = null) =>
        TelemetrySuppression.Evaluate(
            remote,
            selector,
            ignoredAddress,
            ignoredSelectors);

    private static TelemetrySuppressionDecision EvaluateCombined(
        IPAddress remoteAddress,
        string selector) =>
        Evaluate(
            new IPEndPoint(remoteAddress, 12345),
            selector,
            "127.0.0.1",
            ["/healthz"]);

    private static void AssertSuppressed(
        TelemetrySuppressionDecision decision,
        TelemetrySuppressionReason expectedReason)
    {
        Assert.True(decision.IsSuppressed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    private static void AssertNotSuppressed(
        TelemetrySuppressionDecision decision)
    {
        Assert.False(decision.IsSuppressed);
        Assert.Null(decision.Reason);
    }
}
