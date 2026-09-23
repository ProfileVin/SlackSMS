using System.Text;
using Microsoft.AspNetCore.Mvc;
using SlackSms.Application.UseCases;
using SlackSms.Filters;
using SlackSms.Infrastructure.SignalWire;

namespace SlackSms.Controllers;

[ApiController]
[Route("signalwire/sms")]
public sealed class SignalWireSmsController(RelayInboundSms relay) : ControllerBase
{
    private const string EmptyLamlResponse = """<?xml version="1.0" encoding="UTF-8"?><Response></Response>""";

    [HttpPost]
    [VerifySignalWireSignature]
    public async Task<IActionResult> Post([FromForm] IFormCollection form, CancellationToken ct)
    {
        var sms = SignalWireSmsMapper.ToInboundSms(form);

        return await relay.HandleAsync(sms, ct) switch
        {
            RelayResult.Failed => StatusCode(StatusCodes.Status500InternalServerError),
            // An empty <Response> tells SignalWire not to send any automatic reply.
            _ => Content(EmptyLamlResponse, "text/xml", Encoding.UTF8),
        };
    }
}
