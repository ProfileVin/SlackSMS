namespace SlackSms.Application.Abstractions;

public interface IMediaReader
{
    /// <summary>Downloads a text media part of an inbound MMS; null if it can't be read.</summary>
    Task<string?> ReadTextAsync(string url, CancellationToken ct);
}
