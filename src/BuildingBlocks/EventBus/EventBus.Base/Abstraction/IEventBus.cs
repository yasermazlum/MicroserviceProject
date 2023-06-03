using EventBus.Base.Events;

namespace EventBus.Base.Abstraction
{
    public interface IEventBus
    {
        void Publish(IntegrationEvent @event);
        void Subscription<T, TH>(IntegrationEvent @event) where T: IntegrationEvent where TH: IIntegrationEventHandler<T>;
        void UnSubscription<T, TH>(IntegrationEvent @event) where T: IntegrationEvent where TH: IIntegrationEventHandler<T>;
    }
}
