namespace SlackSms.Application.Abstractions;

public interface ISmsSender
{
    /// <summary>Sends an SMS from the bridge number.</summary>
    Task SendSmsAsync(string to, string body, CancellationToken ct);
}

public sealed class SmsSendException(string message, bool isTransient, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>True when retrying may succeed (network error, 429, 5xx).</summary>
    public bool IsTransient { get; } = isTransient;
}
