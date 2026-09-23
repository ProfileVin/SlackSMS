using Microsoft.Extensions.DependencyInjection;
using SlackSms.Application.UseCases;

namespace SlackSms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<QueueChannelMessage>();
        services.AddScoped<ForwardMessageToSms>();
        services.AddScoped<RelayInboundSms>();
        return services;
    }
}
