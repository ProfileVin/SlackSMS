using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SlackSms.Infrastructure.Slack;

namespace SlackSms.Filters;

/// <summary>Rejects requests that aren't signed by Slack, before the body is bound.</summary>
public sealed class VerifySlackSignatureAttribute() : TypeFilterAttribute(typeof(VerifySlackSignatureFilter));

internal sealed class VerifySlackSignatureFilter(
    ISlackRequestVerifier verifier,
    ILogger<VerifySlackSignatureFilter> logger) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        // The signature covers the raw body, so read it here and rewind for model binding.
        request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        if (!verifier.IsValid(
                request.Headers["X-Slack-Request-Timestamp"],
                request.Headers["X-Slack-Signature"],
                body))
        {
            logger.LogWarning("Rejected Slack request with invalid signature");
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
