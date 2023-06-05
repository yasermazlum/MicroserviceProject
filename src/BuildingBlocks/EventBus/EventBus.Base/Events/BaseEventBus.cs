using EventBus.Base.Abstraction;
using EventBus.Base.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Base.Events
{
    public abstract class BaseEventBus : IEventBus
    {
        public readonly IServiceProvider ServiceProvider;
        public readonly IEventBusSubscriptionManager SubscriptionManager;

        private EventBusConfig _config;

        public BaseEventBus(EventBusConfig eventBusConfig, IServiceProvider serviceProvider)
        {
            _config = eventBusConfig;
            ServiceProvider = serviceProvider;
            SubscriptionManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
        }

        public virtual string ProcessEventName(string eventName)
        {
            if(_config.DeleteEventPrefix)
                eventName = eventName.TrimStart(_config.EventNamePrefix.ToArray());

            if(_config.DeleteEventSuffix)
                eventName = eventName.TrimEnd(_config.EventNameSuffix.ToArray());

            return eventName;
        }

        public virtual string GetSubName(string eventName)
        {
            return $"{_config.SubscriberClientAppName}.{ProcessEventName(eventName)}";
        }

        public virtual void Dispose()
        {
            _config = null;
        }
        public async Task<bool> ProcessEvent(string eventName, string message)
        {
            eventName = ProcessEventName(eventName);

            var processed = false;

            if (SubscriptionManager.HashSubscriptionForEvent(eventName))
            {
                var subscriptions = SubscriptionManager.GetHandlerForEvent(eventName);

                using(var scope = ServiceProvider.CreateScope())
                {
                    foreach(var subscription in subscriptions)
                    {
                        var handler = ServiceProvider.GetService(subscription.HandlerType);                        
                        if (handler != null) continue;

                        var eventType = SubscriptionManager.GetEventTypeByName(
                                $"{_config.EventNamePrefix}{eventName}{_config.EventNameSuffix}");
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                        
                        //if (integrationEvent is IntegrationEvent)
                        //    _config.CorrelationIdSetter?.Invoke((integrationEvent as IntegrationEvent).CorrelationId);

                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });

                    }
                }
                processed = true;
            }
            return processed;
        }

        public abstract void Publish(IntegrationEvent @event);

        public abstract void Subscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        public abstract void UnSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;
    }
}
