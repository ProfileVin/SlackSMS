using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlackSms.Infrastructure.SignalWire;

namespace SlackSms.Filters;

/// <summary>Rejects webhooks that aren't signed by SignalWire or belong to another project.</summary>
public sealed class VerifySignalWireSignatureAttribute() : TypeFilterAttribute(typeof(VerifySignalWireSignatureFilter));

internal sealed class VerifySignalWireSignatureFilter(
    ISignalWireRequestVerifier verifier,
    ILogger<VerifySignalWireSignatureFilter> logger) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!request.HasFormContentType)
        {
            context.Result = new BadRequestResult();
            return;
        }

        // The signature covers the form fields; reading here caches them for model binding.
        var form = await request.ReadFormAsync(context.HttpContext.RequestAborted);
        var signature = request.Headers["X-SignalWire-Signature"].FirstOrDefault()
                        ?? request.Headers["X-Twilio-Signature"].FirstOrDefault();

        switch (verifier.Verify(form, signature))
        {
            case SignalWireRequestStatus.InvalidSignature:
                logger.LogWarning("Rejected SignalWire request with invalid signature");
                context.Result = new UnauthorizedResult();
                return;

            case SignalWireRequestStatus.WrongAccount:
                logger.LogWarning("Rejected SignalWire request with mismatched AccountSid");
                context.Result = new UnauthorizedResult();
                return;
        }

        await next();
    }
}
