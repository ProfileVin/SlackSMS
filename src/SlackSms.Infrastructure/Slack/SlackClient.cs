using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.Slack;

/// <summary>
/// Slack Web API client for the bridged channel (chat.postMessage) and name lookups
/// (users.info, conversations.info, usergroups.list).
/// </summary>
public sealed class SlackClient(HttpClient http, IOptions<SlackOptions> options, ILogger<SlackClient> logger)
    : IChannelClient, IUserDirectory, ISlackNameLookup
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

    public Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct) =>
        LookupAsync<SlackUserInfoResponse>($"users.info?user={Uri.EscapeDataString(userId)}", userId, result =>
        {
            var profile = result.User?.Profile;
            return new[] { profile?.DisplayName, profile?.RealName, result.User?.Name }
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        }, ct);

    public Task<string?> GetChannelNameAsync(string channelId, CancellationToken ct) =>
        LookupAsync<SlackConversationInfoResponse>($"conversations.info?channel={Uri.EscapeDataString(channelId)}", channelId,
            result => result.Channel?.Name, ct);

    public Task<string?> GetUserGroupHandleAsync(string groupId, CancellationToken ct) =>
        LookupAsync<SlackUserGroupsResponse>("usergroups.list", groupId,
            result => result.Usergroups?.FirstOrDefault(g => g.Id == groupId)?.Handle, ct);

    /// <summary>GET a Slack API method and pick a name from it; logs and returns null on any failure.</summary>
    private async Task<string?> LookupAsync<TResponse>(
        string pathAndQuery, string id, Func<TResponse, string?> select, CancellationToken ct)
        where TResponse : ISlackResponse
    {
        var method = pathAndQuery.Split('?')[0];
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);

            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TResponse>(ct);
            if (result is not { Ok: true })
            {
                logger.LogWarning("Slack {Method} failed for {Id}: {Error}", method, id, result?.Error);
                return null;
            }

            return select(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Slack {Method} failed for {Id}", method, id);
            return null;
        }
    }

    private interface ISlackResponse
    {
        bool Ok { get; }
        string? Error { get; }
    }

    private sealed record SlackApiResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error) : ISlackResponse;

    private sealed record SlackUserInfoResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("user")] SlackUser? User) : ISlackResponse;

    private sealed record SlackUser(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("profile")] SlackUserProfile? Profile);

    private sealed record SlackUserProfile(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("real_name")] string? RealName);

    private sealed record SlackConversationInfoResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("channel")] SlackChannel? Channel) : ISlackResponse;

    private sealed record SlackChannel(
        [property: JsonPropertyName("name")] string? Name);

    private sealed record SlackUserGroupsResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("usergroups")] IReadOnlyList<SlackUserGroup>? Usergroups) : ISlackResponse;

    private sealed record SlackUserGroup(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("handle")] string? Handle);
}
