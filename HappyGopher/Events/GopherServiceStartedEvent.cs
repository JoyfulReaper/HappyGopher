namespace HappyGopher.Events;

public sealed record GopherServiceStartedEvent(string ListenAddress)
{
    public const string EventName = "happygopher.service.started";
}