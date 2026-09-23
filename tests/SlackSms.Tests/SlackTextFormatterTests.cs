using SlackSms.Infrastructure.Slack;

namespace SlackSms.Tests;

public class SlackTextFormatterTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("a &amp; b &lt;c&gt;", "a & b <c>")]
    [InlineData("see <https://example.com>", "see https://example.com")]
    [InlineData("see <https://example.com|the site>", "see the site (https://example.com)")]
    [InlineData("hi <@U123ABC>", "hi @U123ABC")]
    [InlineData("hi <@U123ABC|bob>", "hi @bob")]
    [InlineData("in <#C0123|general>", "in #general")]
    [InlineData("<!here> meeting", "@here meeting")]
    [InlineData("<!subteam^S123|@devs> ping", "@devs ping")]
    [InlineData("mail <mailto:a@b.com|a@b.com>", "mail a@b.com")]
    public void ToPlainText_converts_slack_markup(string input, string expected) =>
        Assert.Equal(expected, SlackTextFormatter.ToPlainText(input));

    [Fact]
    public void EscapeForSlack_neutralizes_markup() =>
        Assert.Equal("&lt;!channel&gt; &amp; hi", SlackTextFormatter.EscapeForSlack("<!channel> & hi"));
}
