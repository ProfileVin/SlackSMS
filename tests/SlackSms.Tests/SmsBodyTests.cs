using SlackSms.Domain;

namespace SlackSms.Tests;

public class SmsBodyTests
{
    [Fact]
    public void Truncate_limits_length()
    {
        var result = SmsBody.Truncate(new string('x', 2000));
        Assert.Equal(SmsBody.MaxLength, result.Length);
        Assert.EndsWith("…", result);
        Assert.Equal("short", SmsBody.Truncate("short"));
    }

    [Theory]
    [InlineData("  hi  ", false, null, "hi")]
    [InlineData("hi", true, null, "hi [attachment]")]
    [InlineData("", true, null, "[attachment]")]
    [InlineData("hi", false, "Bob", "Bob: hi")]
    [InlineData("hi", true, "Bob", "Bob: hi [attachment]")]
    [InlineData("hi", false, "", "hi")]
    public void Compose_adds_sender_and_attachment_marker(string text, bool hasAttachments, string? sender, string expected) =>
        Assert.Equal(expected, SmsBody.Compose(text, hasAttachments, sender));

    [Fact]
    public void Compose_truncates_after_adding_sender() =>
        Assert.Equal(SmsBody.MaxLength, SmsBody.Compose(new string('x', 2000), false, "Bob").Length);

    [Fact]
    public void Inbound_sms_channel_text_lists_media_urls() =>
        Assert.Equal(
            "SMS from +1555: photo\nhttps://a/1.jpg\nhttps://a/2.jpg",
            new InboundSms("SM1", "+1555", "+1666", "photo", ["https://a/1.jpg", "https://a/2.jpg"]).ToChannelText());
}
