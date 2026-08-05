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
    public void IsIgnoredTelemetrySource_NativeIPv4MatchesConfiguredIPv4()
    {
        IPEndPoint remote = new(IPAddress.Parse("192.0.2.10"), 12345);

        Assert.True(TelemetryService.IsIgnoredTelemetrySource(
            remote,
            "192.0.2.10"));
    }

    [Fact]
    public void IsIgnoredTelemetrySource_MappedIPv4MatchesConfiguredIPv4()
    {
        IPEndPoint remote = new(
            IPAddress.Parse("::ffff:192.0.2.10"),
            12345);

        Assert.True(TelemetryService.IsIgnoredTelemetrySource(
            remote,
            "192.0.2.10"));
    }

    [Fact]
    public void IsIgnoredTelemetrySource_NativeIPv6MatchesConfiguredIPv6()
    {
        IPEndPoint remote = new(IPAddress.Parse("2001:db8::10"), 12345);

        Assert.True(TelemetryService.IsIgnoredTelemetrySource(
            remote,
            "2001:db8::10"));
    }

    [Fact]
    public void IsIgnoredTelemetrySource_DifferentIPv6DoesNotMatch()
    {
        IPEndPoint remote = new(IPAddress.Parse("2001:db8::10"), 12345);

        Assert.False(TelemetryService.IsIgnoredTelemetrySource(
            remote,
            "2001:db8::11"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public void IsIgnoredTelemetrySource_InvalidConfigurationDoesNotMatch(
        string? ignoredAddress)
    {
        IPEndPoint remote = new(IPAddress.Loopback, 12345);

        Assert.False(TelemetryService.IsIgnoredTelemetrySource(
            remote,
            ignoredAddress));
    }

    [Fact]
    public void IsIgnoredTelemetrySource_NullEndpointDoesNotMatch()
    {
        Assert.False(TelemetryService.IsIgnoredTelemetrySource(
            null,
            "127.0.0.1"));
    }

    [Fact]
    public void IsIgnoredTelemetrySource_NonIpEndpointDoesNotMatch()
    {
        DnsEndPoint remote = new("localhost", 12345);

        Assert.False(TelemetryService.IsIgnoredTelemetrySource(
            remote,
            "127.0.0.1"));
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
}
