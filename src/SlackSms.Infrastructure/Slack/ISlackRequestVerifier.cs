namespace SlackSms.Infrastructure.Slack;

/// <summary>Checks that a request really came from Slack, using the configured signing secret.</summary>
public interface ISlackRequestVerifier
{
    bool IsValid(string? timestampHeader, string? signatureHeader, string body);
}
