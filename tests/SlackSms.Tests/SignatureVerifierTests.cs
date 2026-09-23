using SlackSms.Infrastructure.SignalWire;
using SlackSms.Infrastructure.Slack;

namespace SlackSms.Tests;

public class SlackSignatureVerifierTests
{
    // Example from https://api.slack.com/authentication/verifying-requests-from-slack
    private const string DocsSecret = "8f742231b10e8888abcd99yyyzzz85a5";
    private const string DocsTimestamp = "1531420618";
    private const string DocsBody =
        "token=xyzz0WbapA4vBCDEFasx0q6G&team_id=T1DC2JH3J&team_domain=testteamnow&channel_id=G8PSS9T3V&channel_name=foobar&user_id=U2CERLKJA&user_name=roadrunner&command=%2Fwebhook-collect&text=&response_url=https%3A%2F%2Fhooks.slack.com%2Fcommands%2FT1DC2JH3J%2F397700885554%2F96rGlfmibIGlgcZRskXaIFfN&trigger_id=398738663015.47445629121.803a0bc887a14d10d2c447fce8b6703c";
    private const string DocsSignature = "v0=a2114d57b48eac39b9ad189dd8316235a7b4a8d21a10bd27519666489c69b503";
    private static readonly DateTimeOffset DocsNow = DateTimeOffset.FromUnixTimeSeconds(1531420618 + 30);

    [Fact]
    public void Accepts_slack_documentation_example() =>
        Assert.True(SlackSignatureVerifier.IsValid(DocsSecret, DocsTimestamp, DocsSignature, DocsBody, DocsNow));

    [Fact]
    public void Rejects_tampered_body() =>
        Assert.False(SlackSignatureVerifier.IsValid(DocsSecret, DocsTimestamp, DocsSignature, DocsBody + "x", DocsNow));

    [Fact]
    public void Rejects_wrong_secret() =>
        Assert.False(SlackSignatureVerifier.IsValid("other-secret", DocsTimestamp, DocsSignature, DocsBody, DocsNow));

    [Fact]
    public void Rejects_stale_timestamp() =>
        Assert.False(SlackSignatureVerifier.IsValid(DocsSecret, DocsTimestamp, DocsSignature, DocsBody, DocsNow.AddMinutes(10)));

    [Theory]
    [InlineData(null, DocsSignature)]
    [InlineData(DocsTimestamp, null)]
    [InlineData("not-a-number", DocsSignature)]
    public void Rejects_missing_or_malformed_headers(string? timestamp, string? signature) =>
        Assert.False(SlackSignatureVerifier.IsValid(DocsSecret, timestamp, signature, DocsBody, DocsNow));
}

public class SignalWireSignatureVerifierTests
{
    // Twilio-compatible example (SignalWire uses the same scheme for LaML webhooks).
    private const string Key = "12345";
    private const string Url = "https://mycompany.com/myapp.php?foo=1&bar=2";

    private static readonly Dictionary<string, IEnumerable<string>> Params = new()
    {
        ["CallSid"] = ["CA1234567890ABCDE"],
        ["Caller"] = ["+12349013030"],
        ["Digits"] = ["1234"],
        ["From"] = ["+12349013030"],
        ["To"] = ["+18005551212"],
    };

    [Fact]
    public void Accepts_known_example() =>
        Assert.True(SignalWireSignatureVerifier.IsValid(Key, Url, Params, "0/KCTR6DLpKmkAf8muzZqo1nDgQ="));

    [Fact]
    public void Parameter_order_does_not_matter()
    {
        var reversed = Params.Reverse().ToDictionary();
        Assert.Equal(
            SignalWireSignatureVerifier.ComputeSignature(Key, Url, Params),
            SignalWireSignatureVerifier.ComputeSignature(Key, Url, reversed));
    }

    [Fact]
    public void Rejects_modified_parameter()
    {
        var tampered = new Dictionary<string, IEnumerable<string>>(Params) { ["Digits"] = ["9999"] };
        Assert.False(SignalWireSignatureVerifier.IsValid(Key, Url, tampered, "0/KCTR6DLpKmkAf8muzZqo1nDgQ="));
    }

    [Fact]
    public void Rejects_different_url() =>
        Assert.False(SignalWireSignatureVerifier.IsValid(Key, "https://mycompany.com/other", Params, "0/KCTR6DLpKmkAf8muzZqo1nDgQ="));

    [Fact]
    public void Rejects_wrong_key_and_missing_signature()
    {
        Assert.False(SignalWireSignatureVerifier.IsValid("wrong", Url, Params, "0/KCTR6DLpKmkAf8muzZqo1nDgQ="));
        Assert.False(SignalWireSignatureVerifier.IsValid(Key, Url, Params, null));
    }
}
