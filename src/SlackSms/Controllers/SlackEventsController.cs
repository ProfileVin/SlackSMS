using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SlackSms.Application.UseCases;
using SlackSms.Filters;
using SlackSms.Infrastructure.Slack;

namespace SlackSms.Controllers;

[ApiController]
[Route("slack/events")]
public sealed class SlackEventsController(
    QueueChannelMessage queueMessage,
    IOptions<SlackOptions> options) : ControllerBase
{
    [HttpPost]
    [VerifySlackSignature]
    public async Task<IActionResult> Post([FromBody] SlackEnvelope envelope, CancellationToken ct)
    {
        switch (envelope.Type)
        {
            case "url_verification":
                return Ok(new { challenge = envelope.Challenge });

            case "event_callback" when envelope.Event is { } evt && envelope.EventId is { } eventId:
                if (SlackEventMapper.TryMap(evt, eventId, options.Value.ChannelId, out var message))
                    await queueMessage.HandleAsync(message, ct);
                // Acknowledge immediately: Slack expects a response within 3 seconds.
                return Ok();

            default:
                return Ok();
        }
    }
}
