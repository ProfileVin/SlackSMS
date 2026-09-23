using System.Net;
using System.Text.Json;

namespace SlackSms.Tests;

/// <summary>Responses that come from the MVC pipeline rather than from a use case.</summary>
public class ControllerBehaviourTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public ControllerBehaviourTests() => _client = _app.CreateClient();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Health_returns_plain_text_ok()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Malformed_slack_json_is_a_bad_request()
    {
        var response = await _client.SendAsync(TestApp.SignedSlackRequest("{ not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await _app.Sms.AnyMoreAsync());
    }

    [Fact]
    public async Task Unsigned_request_is_rejected_before_the_body_is_parsed()
    {
        var response = await _client.PostAsync("/slack/events", new StringContent("{ not json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task File_share_event_without_text_is_forwarded_as_an_attachment()
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "event_callback",
            event_id = "EvFile",
            @event = new
            {
                type = "message",
                subtype = "file_share",
                channel = TestApp.SlackChannelId,
                user = "U123",
                text = "",
                ts = "1.0",
                files = new[] { new { id = "F1", name = "pic.png" } },
            },
        });

        (await _client.SendAsync(TestApp.SignedSlackRequest(json))).EnsureSuccessStatusCode();

        Assert.Equal("[attachment]", (await _app.Sms.NextAsync()).Body);
    }
}
