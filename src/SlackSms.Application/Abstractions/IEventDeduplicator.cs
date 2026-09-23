namespace SlackSms.Application.Abstractions;

/// <summary>Chat platforms retry events they think we missed; remember event IDs so each is handled once.</summary>
public interface IEventDeduplicator
{
    /// <summary>Returns true the first time an event ID is seen.</summary>
    bool TryRegister(string eventId);
}
