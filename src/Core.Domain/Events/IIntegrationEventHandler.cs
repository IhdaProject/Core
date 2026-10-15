namespace Core.Domain.Events;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task Handle(TEvent @event, CancellationToken cancellationToken = default);
}
