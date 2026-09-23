using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlackSms.Application;
using SlackSms.Application.Abstractions;
using SlackSms.Infrastructure.Messaging;
using SlackSms.Infrastructure.SignalWire;
using SlackSms.Infrastructure.Slack;

namespace SlackSms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<SlackOptions>()
            .BindConfiguration(SlackOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SignalWireOptions>()
            .BindConfiguration(SignalWireOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<BridgeOptions>()
            .Configure<IOptions<SlackOptions>, IOptions<SignalWireOptions>>((bridge, slack, signalWire) =>
            {
                bridge.OwnNumber = signalWire.Value.FromNumber;
                bridge.DestinationNumber = signalWire.Value.DestinationNumber;
                bridge.IncludeSenderName = slack.Value.IncludeSenderName;
            });

        services.AddHttpClient<SlackClient>(http =>
        {
            http.BaseAddress = new Uri("https://slack.com/api/");
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddTransient<IChannelClient>(sp => sp.GetRequiredService<SlackClient>());
        services.AddTransient<IUserDirectory>(sp =>
            new CachedUserDirectory(sp.GetRequiredService<SlackClient>(), sp.GetRequiredService<IMemoryCache>()));

        services.AddHttpClient<ISmsSender, SignalWireClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<SignalWireOptions>>().Value;
            http.BaseAddress = new Uri(SignalWireClient.GetBaseAddress(options.Space));
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<ISlackRequestVerifier, SlackRequestVerifier>();
        services.AddSingleton<ISignalWireRequestVerifier, SignalWireRequestVerifier>();

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOutboundMessageQueue, InMemoryOutboundQueue>();
        services.AddSingleton<IEventDeduplicator, MemoryCacheEventDeduplicator>();
        services.AddHostedService<SmsForwardingWorker>();

        return services;
    }
}
