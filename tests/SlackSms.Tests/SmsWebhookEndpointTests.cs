using System.Net;

namespace SlackSms.Tests;

public class SmsWebhookEndpointTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public SmsWebhookEndpointTests() => _client = _app.CreateClient();

    public void Dispose() => _app.Dispose();

    private static Dictionary<string, string> InboundSms(string body, string to = TestApp.FromNumber) => new()
    {
        ["MessageSid"] = "SM123",
        ["AccountSid"] = "project-id",
        ["From"] = "+15551234567",
        ["To"] = to,
        ["Body"] = body,
        ["NumMedia"] = "0",
    };

    [Fact]
    public async Task Inbound_sms_is_posted_to_slack_with_empty_laml()
    {
        var response = await _client.SendAsync(TestApp.SignedSmsRequest(InboundSms("Hi <!channel>")));

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<Response></Response>", await response.Content.ReadAsStringAsync());
        Assert.Equal(["SMS from +15551234567: Hi <!channel>"], _app.Slack.Posted);
    }

    [Fact]
    public async Task Media_urls_are_included()
    {
        var form = InboundSms("photo");
        form["NumMedia"] = "1";
        form["MediaUrl0"] = "https://files.example.com/pic.jpg";

        (await _client.SendAsync(TestApp.SignedSmsRequest(form))).EnsureSuccessStatusCode();

        Assert.Equal(["SMS from +15551234567: photo\nhttps://files.example.com/pic.jpg"], _app.Slack.Posted);
    }

    [Fact]
    public async Task Invalid_signature_is_rejected()
    {
        var response = await _client.SendAsync(TestApp.SignedSmsRequest(InboundSms("hi"), signingKey: "wrong-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_app.Slack.Posted);
    }

    [Fact]
    public async Task Signed_request_from_other_project_is_rejected()
    {
        var form = InboundSms("hi");
        form["AccountSid"] = "someone-elses-project";

        var response = await _client.SendAsync(TestApp.SignedSmsRequest(form));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_app.Slack.Posted);
    }

    [Fact]
    public async Task Sms_to_other_number_is_ignored()
    {
        var response = await _client.SendAsync(TestApp.SignedSmsRequest(InboundSms("hi", to: "+15559999999")));

        response.EnsureSuccessStatusCode();
        Assert.Empty(_app.Slack.Posted);
    }

    [Fact]
    public async Task Slack_failure_returns_500()
    {
        _app.Slack.Fail = true;
        var response = await _client.SendAsync(TestApp.SignedSmsRequest(InboundSms("hi")));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
