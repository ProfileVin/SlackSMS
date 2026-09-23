namespace SlackSms.Application.Abstractions;

/// <summary>The bridged chat channel.</summary>
public interface IChannelClient
{
    /// <summary>Posts plain text to the bridged channel; the implementation escapes it for the chat platform.</summary>
    Task PostAsync(string plainText, CancellationToken ct);
}
