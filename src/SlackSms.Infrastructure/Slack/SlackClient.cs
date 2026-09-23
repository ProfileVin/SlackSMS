using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.Slack;

/// <summary>Slack Web API client for the bridged channel (chat.postMessage, users.info).</summary>
public sealed class SlackClient(HttpClient http, IOptions<SlackOptions> options, ILogger<SlackClient> logger)
    : IChannelClient, IUserDirectory
{
    private readonly SlackOptions _options = options.Value;

    public async Task PostAsync(string plainText, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat.postMessage")
        {
            Content = JsonContent.Create(new
            {
                channel = _options.ChannelId,
                text = SlackTextFormatter.EscapeForSlack(plainText),
                unfurl_links = false,
                unfurl_media = false,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        // Slack reports API errors with HTTP 200 and ok=false.
        var result = await response.Content.ReadFromJsonAsync<SlackApiResponse>(ct);
        if (result is not { Ok: true })
            throw new InvalidOperationException($"Slack chat.postMessage failed: {result?.Error ?? "empty response"}");
    }

    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"users.info?user={Uri.EscapeDataString(userId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);

            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SlackUserInfoResponse>(ct);
            if (result is not { Ok: true, User: not null })
            {
                logger.LogWarning("Slack users.info failed for {UserId}: {Error}", userId, result?.Error);
                return null;
            }

            var profile = result.User.Profile;
            return new[] { profile?.DisplayName, profile?.RealName, result.User.Name }
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Slack users.info failed for {UserId}", userId);
            return null;
        }
    }

    private sealed record SlackApiResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record SlackUserInfoResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("user")] SlackUser? User);

    private sealed record SlackUser(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("profile")] SlackUserProfile? Profile);

    private sealed record SlackUserProfile(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("real_name")] string? RealName);
}
