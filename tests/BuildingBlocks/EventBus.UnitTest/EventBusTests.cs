using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factory;
using EventBus.UnitTest.Events.EventHandlers;
using EventBus.UnitTest.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EventBus.UnitTest
{
    [TestClass]
    public class EventBusTests
    {

        private ServiceCollection _services;

        public EventBusTests()
        {
            _services = new ServiceCollection();
            _services.AddLogging( conf => conf.AddConsole());
        }

        [TestMethod]
        public void SubscribeEventOnRabbitMQTest()
        {
            _services.AddSingleton<IEventBus>(sp =>
            {
                EventBusConfig config = new EventBusConfig()
                {
                    ConnectionRetryCount = 5,
                    SubscriberClientAppName = "EventBus.UnitTest",
                    DefaultTopicName = "MicroserviceProjectTopicName",
                    EventBusType = EventBusType.RabbitMQ,
                    EventNameSuffix = "IntegrationEvent",
                    //Connection = new ConnectionFactory()
                    //{
                    //    HostName = "localhost",
                    //    Port = 15672,
                    //    UserName = "guest",
                    //    Password = "guest"
                    //}
                };
                return EventBusFactory.Create(config, sp);
            });

            var sp = _services.BuildServiceProvider();

            var eventBus = sp.GetRequiredService<IEventBus>();

            eventBus.Subscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
            eventBus.UnSubscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
        }

        [TestMethod]
        public void SubscribeEventOnAzureTest()
        {
            _services.AddSingleton<IEventBus>(sp =>
            {
                EventBusConfig config = new EventBusConfig()
                {
                    ConnectionRetryCount = 5,
                    SubscriberClientAppName = "EventBus.UnitTest",
                    DefaultTopicName = "MicroserviceProjectTopicName",
                    EventBusType = EventBusType.RabbitMQ,
                    EventNameSuffix = "IntegrationEvent",
                    EventBusConnectionString=""
                };
                return EventBusFactory.Create(config, sp);
            });

            var sp = _services.BuildServiceProvider();

            var eventBus = sp.GetRequiredService<IEventBus>();

            eventBus.Subscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
            eventBus.UnSubscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
        }
    }
}
}