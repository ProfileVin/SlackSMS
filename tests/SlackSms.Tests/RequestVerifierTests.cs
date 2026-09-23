using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SlackSms.Infrastructure.SignalWire;
using SlackSms.Infrastructure.Slack;

namespace SlackSms.Tests;

/// <summary>A clock frozen at a chosen instant, so signature ages are deterministic.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class SlackRequestVerifierTests
{
    private const string Secret = "test-slack-secret";
    private const string Body = """{"type":"url_verification","challenge":"abc"}""";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    private static SlackRequestVerifier Create(DateTimeOffset? now = null) =>
        new(Options.Create(new SlackOptions { SigningSecret = Secret }), new FixedTimeProvider(now ?? Now));

    private static (string Timestamp, string Signature) Sign(string body, DateTimeOffset at)
    {
        var ts = at.ToUnixTimeSeconds().ToString();
        var hash = System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Secret),
            System.Text.Encoding.UTF8.GetBytes($"v0:{ts}:{body}"));
        return (ts, "v0=" + Convert.ToHexStringLower(hash));
    }

    [Fact]
    public void Accepts_request_signed_with_the_configured_secret()
    {
        var (ts, signature) = Sign(Body, Now);
        Assert.True(Create().IsValid(ts, signature, Body));
    }

    [Fact]
    public void Rejects_tampered_body()
    {
        var (ts, signature) = Sign(Body, Now);
        Assert.False(Create().IsValid(ts, signature, Body + "x"));
    }

    [Fact]
    public void Rejects_signature_that_is_too_old_for_the_clock()
    {
        var (ts, signature) = Sign(Body, Now);
        Assert.False(Create(Now.AddMinutes(10)).IsValid(ts, signature, Body));
    }

    [Fact]
    public void Rejects_missing_headers() =>
        Assert.False(Create().IsValid(null, null, Body));
}

public class SignalWireRequestVerifierTests
{
    private const string SigningKey = "test-signalwire-key";
    private const string ProjectId = "project-id";
    private const string WebhookUrl = "https://bridge.example.com/signalwire/sms";

    private static readonly Dictionary<string, string> Form = new()
    {
        ["MessageSid"] = "SM123",
        ["AccountSid"] = ProjectId,
        ["From"] = "+15551234567",
        ["To"] = "+15550000001",
        ["Body"] = "hi",
        ["NumMedia"] = "0",
    };

    private static SignalWireRequestVerifier Create(bool validateSignature = true) =>
        new(Options.Create(new SignalWireOptions
        {
            SigningKey = SigningKey,
            ProjectId = ProjectId,
            WebhookPublicUrl = WebhookUrl,
            ValidateSignature = validateSignature,
        }));

    private static IFormCollection AsForm(Dictionary<string, string> values) =>
        new FormCollection(values.ToDictionary(v => v.Key, v => new StringValues(v.Value)));

    private static string SignatureFor(Dictionary<string, string> values, string key = SigningKey) =>
        SignalWireSignatureVerifier.ComputeSignature(
            key, WebhookUrl, values.Select(v => new KeyValuePair<string, IEnumerable<string>>(v.Key, [v.Value])));

    [Fact]
    public void Accepts_signed_webhook_from_our_project() =>
        Assert.Equal(
            SignalWireRequestStatus.Valid,
            Create().Verify(AsForm(Form), SignatureFor(Form)));

    [Fact]
    public void Rejects_signature_made_with_another_key() =>
        Assert.Equal(
            SignalWireRequestStatus.InvalidSignature,
            Create().Verify(AsForm(Form), SignatureFor(Form, key: "wrong-key")));

    [Fact]
    public void Rejects_missing_signature() =>
        Assert.Equal(
            SignalWireRequestStatus.InvalidSignature,
            Create().Verify(AsForm(Form), signature: null));

    [Fact]
    public void Rejects_signed_webhook_from_another_project()
    {
        var other = new Dictionary<string, string>(Form) { ["AccountSid"] = "someone-elses-project" };

        Assert.Equal(
            SignalWireRequestStatus.WrongAccount,
            Create().Verify(AsForm(other), SignatureFor(other)));
    }

    [Fact]
    public void Account_is_still_checked_when_signature_validation_is_off()
    {
        var other = new Dictionary<string, string>(Form) { ["AccountSid"] = "someone-elses-project" };
        var verifier = Create(validateSignature: false);

        Assert.Equal(SignalWireRequestStatus.Valid, verifier.Verify(AsForm(Form), signature: null));
        Assert.Equal(SignalWireRequestStatus.WrongAccount, verifier.Verify(AsForm(other), signature: null));
    }
}
