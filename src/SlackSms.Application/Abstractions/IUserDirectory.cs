namespace SlackSms.Application.Abstractions;

public interface IUserDirectory
{
    /// <summary>Best-effort display name lookup; returns null when unavailable.</summary>
    Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct);
}
