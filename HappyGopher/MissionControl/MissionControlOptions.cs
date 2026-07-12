namespace HappyGopher.MissionControl;

public sealed class MissionControlOptions
{
    public const string SectionName = "MissionControl";
    public const string HttpClientName = "MissionControl";

    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } =
        "http://localhost:5190";

    public string ApiKey { get; init; } =
        string.Empty;

    public int TimeoutMilliseconds { get; init; } =
        1000;
}