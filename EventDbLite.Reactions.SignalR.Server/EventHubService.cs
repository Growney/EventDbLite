using EventDbLite.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EventDbLite.Reactions.SignalR.Server;
public class EventHubService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<Type> _requirements;
    private readonly IHubContext<EventsHub> _hubContext;
    private readonly ILogger<EventHubService> _logger;

    public EventHubService(IServiceProvider serviceProvider, IEnumerable<Type> requirements, IHubContext<EventsHub> hubContext, ILogger<EventHubService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        IEventStoreLite store = scope.ServiceProvider.GetRequiredService<IEventStoreLite>();

        IStreamSubscription subscription = store.SubscribeToAllStreams(Position.End);

        IEventSerializer serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();

        await foreach (SubscriptionMessage subscriptionMessage in subscription.Messages(stoppingToken))
        {
            if(subscriptionMessage is not SubscriptionMessage.Event eventMessage)
            {
                continue;
            }

            StreamEvent streamEvent = eventMessage.SubscriptionEvent;

            _logger.LogInformation("Broadcasting event {Identifier} with GlobalOrdinal {GlobalOrdinal}", streamEvent.Data.Identifier, streamEvent.GlobalOrdinal);
            await _hubContext.Clients.All.SendAsync("ReceiveEvent", streamEvent);

        }
    }
}
