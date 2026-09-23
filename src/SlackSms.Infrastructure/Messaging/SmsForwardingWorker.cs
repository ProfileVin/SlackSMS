using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SlackSms.Application.Abstractions;
using SlackSms.Application.UseCases;

namespace SlackSms.Infrastructure.Messaging;

/// <summary>Drains the outbound queue, forwarding each message as SMS in its own DI scope.</summary>
public sealed class SmsForwardingWorker(
    IOutboundMessageQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SmsForwardingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var forward = scope.ServiceProvider.GetRequiredService<ForwardMessageToSms>();
                await forward.HandleAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Giving up on Slack event {EventId}; SMS not delivered", message.Id);
            }
        }
    }
}
