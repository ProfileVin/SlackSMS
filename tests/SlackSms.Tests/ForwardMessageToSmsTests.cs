using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlackSms.Application;
using SlackSms.Application.Abstractions;
using SlackSms.Application.UseCases;
using SlackSms.Domain;

namespace SlackSms.Tests;

public class ForwardMessageToSmsTests
{
    private const string Destination = "+15550000002";

    private static ForwardMessageToSms Create(ISmsSender sms, bool includeSenderName = false) =>
        new(sms, new FakeSlackClient(),
            Options.Create(new BridgeOptions
            {
                DestinationNumber = Destination,
                IncludeSenderName = includeSenderName,
                SmsRetryDelays = [TimeSpan.Zero, TimeSpan.Zero],
            }),
            TimeProvider.System,
            NullLogger<ForwardMessageToSms>.Instance);

    [Fact]
    public async Task Sends_to_destination_with_sender_name_when_enabled()
    {
        var sms = new FakeSmsSender();
        await Create(sms, includeSenderName: true).HandleAsync(new ChannelMessage("Ev1", "U1", "hello", true), CancellationToken.None);

        Assert.Equal((Destination, "Bob: hello [attachment]"), await sms.NextAsync());
    }

    [Fact]
    public async Task Omits_sender_name_when_disabled()
    {
        var sms = new FakeSmsSender();
        await Create(sms).HandleAsync(new ChannelMessage("Ev1", "U1", "hello", false), CancellationToken.None);

        Assert.Equal("hello", (await sms.NextAsync()).Body);
    }

    [Fact]
    public async Task Retries_transient_failures_then_succeeds()
    {
        var sms = new FlakySmsSender(failures: 2, transient: true);
        await Create(sms).HandleAsync(new ChannelMessage("Ev1", null, "hi", false), CancellationToken.None);

        Assert.Equal(3, sms.Attempts);
    }

    [Fact]
    public async Task Gives_up_after_retries_are_exhausted()
    {
        var sms = new FlakySmsSender(failures: 10, transient: true);
        await Assert.ThrowsAsync<SmsSendException>(() =>
            Create(sms).HandleAsync(new ChannelMessage("Ev1", null, "hi", false), CancellationToken.None));

        Assert.Equal(3, sms.Attempts); // first try + two configured retries
    }

    [Fact]
    public async Task Does_not_retry_permanent_failures()
    {
        var sms = new FlakySmsSender(failures: 1, transient: false);
        await Assert.ThrowsAsync<SmsSendException>(() =>
            Create(sms).HandleAsync(new ChannelMessage("Ev1", null, "hi", false), CancellationToken.None));

        Assert.Equal(1, sms.Attempts);
    }

    private sealed class FlakySmsSender(int failures, bool transient) : ISmsSender
    {
        public int Attempts { get; private set; }

        public Task SendSmsAsync(string to, string body, CancellationToken ct)
        {
            Attempts++;
            if (Attempts <= failures)
                throw new SmsSendException("boom", transient);
            return Task.CompletedTask;
        }
    }
}
