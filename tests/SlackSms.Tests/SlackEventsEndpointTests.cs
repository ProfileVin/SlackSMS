using System.Net;
using System.Text.Json;

namespace SlackSms.Tests;

public class SlackEventsEndpointTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public SlackEventsEndpointTests() => _client = _app.CreateClient();

    public void Dispose() => _app.Dispose();

    private static string MessageEvent(
        string eventId, string text, string channel = TestApp.SlackChannelId,
        string? subtype = null, string? botId = null, string user = "U123") =>
        JsonSerializer.Serialize(new
        {
            type = "event_callback",
            event_id = eventId,
            @event = new { type = "message", channel, user, text, subtype, bot_id = botId, ts = "1.0" },
        });

    [Fact]
    public async Task Url_verification_returns_challenge()
    {
        var json = """{"type":"url_verification","challenge":"abc123","token":"x"}""";
        var response = await _client.SendAsync(TestApp.SignedSlackRequest(json));

        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("abc123", doc.RootElement.GetProperty("challenge").GetString());
    }

    [Fact]
    public async Task Invalid_signature_is_rejected()
    {
        var request = TestApp.SignedSlackRequest(MessageEvent("Ev1", "hi"));
        request.Headers.Remove("X-Slack-Signature");
        request.Headers.Add("X-Slack-Signature", "v0=deadbeef");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(await _app.Sms.AnyMoreAsync());
    }

    [Fact]
    public async Task Old_timestamp_is_rejected()
    {
        var old = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var response = await _client.SendAsync(TestApp.SignedSlackRequest(MessageEvent("Ev1", "hi"), old));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Channel_message_is_sent_as_sms_to_destination()
    {
        var response = await _client.SendAsync(TestApp.SignedSlackRequest(MessageEvent("Ev1", "Hello &amp; <https://x.io|link>")));
        response.EnsureSuccessStatusCode();

        var sms = await _app.Sms.NextAsync();
        Assert.Equal(TestApp.DestinationNumber, sms.To);
        Assert.Equal("Hello & link (https://x.io)", sms.Body);
    }

    [Fact]
    public async Task Bot_edits_and_other_channel_messages_are_ignored()
    {
        await _client.SendAsync(TestApp.SignedSlackRequest(MessageEvent("Ev1", "from bot", botId: "B1")));
        await _client.SendAsync(TestApp.SignedSlackRequest(MessageEvent("Ev2", "edited", subtype: "message_changed")));
        await _client.SendAsync(TestApp.SignedSlackRequest(MessageEvent("Ev3", "elsewhere", channel: "C0OTHER")));
        await _client.SendAsync(TestApp.SignedSlackRequest(MessageEvent("Ev4", "real one")));

        var sms = await _app.Sms.NextAsync();
        Assert.Equal("real one", sms.Body);
        Assert.False(await _app.Sms.AnyMoreAsync());
    }

    [Fact]
    public async Task Retried_event_is_sent_only_once()
    {
        var json = MessageEvent("EvDup", "once");
        await _client.SendAsync(TestApp.SignedSlackRequest(json));
        var retry = TestApp.SignedSlackRequest(json);
        retry.Headers.Add("X-Slack-Retry-Num", "1");
        (await _client.SendAsync(retry)).EnsureSuccessStatusCode();

        Assert.Equal("once", (await _app.Sms.NextAsync()).Body);
        Assert.False(await _app.Sms.AnyMoreAsync());
    }
}
