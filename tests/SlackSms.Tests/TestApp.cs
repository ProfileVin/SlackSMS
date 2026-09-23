using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SlackSms.Application.Abstractions;
using SlackSms.Infrastructure.SignalWire;

namespace SlackSms.Tests;

/// <summary>Runs the real app in memory with fake Slack/SignalWire clients.</summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string SlackSigningSecret = "test-slack-secret";
    public const string SlackChannelId = "C0TEST";
    public const string SignalWireSigningKey = "test-signalwire-key";
    public const string FromNumber = "+15550000001";
    public const string DestinationNumber = "+15550000002";
    public const string WebhookUrl = "https://bridge.example.com/signalwire/sms";

    public FakeSmsSender Sms { get; } = new();
    public FakeSlackClient Slack { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Slack:BotToken", "xoxb-test");
        builder.UseSetting("Slack:SigningSecret", SlackSigningSecret);
        builder.UseSetting("Slack:ChannelId", SlackChannelId);
        builder.UseSetting("SignalWire:Space", "test");
        builder.UseSetting("SignalWire:ProjectId", "project-id");
        builder.UseSetting("SignalWire:ApiToken", "api-token");
        builder.UseSetting("SignalWire:SigningKey", SignalWireSigningKey);
        builder.UseSetting("SignalWire:FromNumber", FromNumber);
        builder.UseSetting("SignalWire:DestinationNumber", DestinationNumber);
        builder.UseSetting("SignalWire:WebhookPublicUrl", WebhookUrl);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmsSender>();
            services.RemoveAll<IChannelClient>();
            services.RemoveAll<IUserDirectory>();
            services.AddSingleton<ISmsSender>(Sms);
            services.AddSingleton<IChannelClient>(Slack);
            services.AddSingleton<IUserDirectory>(Slack);
        });
    }

    public static HttpRequestMessage SignedSlackRequest(string json, long? timestamp = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(SlackSigningSecret), Encoding.UTF8.GetBytes($"v0:{ts}:{json}"));

        var request = new HttpRequestMessage(HttpMethod.Post, "/slack/events")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Slack-Request-Timestamp", ts);
        request.Headers.Add("X-Slack-Signature", "v0=" + Convert.ToHexStringLower(hash));
        return request;
    }

    public static HttpRequestMessage SignedSmsRequest(Dictionary<string, string> form, string? signingKey = null)
    {
        var signature = SignalWireSignatureVerifier.ComputeSignature(
            signingKey ?? SignalWireSigningKey,
            WebhookUrl,
            form.Select(f => new KeyValuePair<string, IEnumerable<string>>(f.Key, [f.Value])));

        var request = new HttpRequestMessage(HttpMethod.Post, "/signalwire/sms")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Add("X-SignalWire-Signature", signature);
        return request;
    }
}

public sealed class FakeSmsSender : ISmsSender
{
    private readonly Channel<(string To, string Body)> _sent = Channel.CreateUnbounded<(string, string)>();

    public Task SendSmsAsync(string to, string body, CancellationToken ct)
    {
        _sent.Writer.TryWrite((to, body));
        return Task.CompletedTask;
    }

    public async Task<(string To, string Body)> NextAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _sent.Reader.ReadAsync(cts.Token);
    }

    /// <summary>True if another SMS arrives within a short grace period.</summary>
    public async Task<bool> AnyMoreAsync()
    {
        await Task.Delay(300);
        return _sent.Reader.TryRead(out _);
    }
}

public sealed class FakeSlackClient : IChannelClient, IUserDirectory
{
    public List<string> Posted { get; } = [];
    public bool Fail { get; set; }

    public Task PostAsync(string plainText, CancellationToken ct)
    {
        if (Fail)
            throw new InvalidOperationException("Slack down");
        lock (Posted)
            Posted.Add(plainText);
        return Task.CompletedTask;
    }

    public Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct) => Task.FromResult<string?>("Bob");
}
