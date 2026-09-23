using Microsoft.AspNetCore.Http;
using SlackSms.Domain;

namespace SlackSms.Infrastructure.SignalWire;

/// <summary>Turns a SignalWire LaML webhook form into an inbound SMS.</summary>
public static class SignalWireSmsMapper
{
    public static InboundSms ToInboundSms(IFormCollection form)
    {
        var mediaUrls = new List<string>();
        if (int.TryParse(form["NumMedia"], out var numMedia))
        {
            for (var i = 0; i < numMedia; i++)
                mediaUrls.Add(form[$"MediaUrl{i}"].ToString());
        }

        return new InboundSms(
            form["MessageSid"].ToString(),
            form["From"].ToString(),
            form["To"].ToString(),
            form["Body"].ToString(),
            mediaUrls);
    }
}
