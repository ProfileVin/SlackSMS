namespace SlackSms.Application.Abstractions;

public interface IMessageTextRenderer
{
    /// <summary>Chat markup to plain SMS text, with user and channel mentions resolved to names where possible.</summary>
    Task<string> ToPlainTextAsync(string text, CancellationToken ct);
}
