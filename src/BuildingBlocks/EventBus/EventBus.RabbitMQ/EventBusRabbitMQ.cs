﻿using EventBus.Base;
using EventBus.Base.Events;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using System.Text;

namespace EventBus.RabbitMQ
{
    public class EventBusRabbitMQ : BaseEventBus
    {
        RabbitMQPersistentConnection persistentConnection;
        private readonly IConnectionFactory connectionFactory;
        private readonly IModel consumerChannel;
        public EventBusRabbitMQ(EventBusConfig eventBusConfig, IServiceProvider serviceProvider) : base(eventBusConfig, serviceProvider)
        {
            if (eventBusConfig != null)
            {
                var connJson = JsonConvert.SerializeObject(EventBusConfig.Connection, new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                connectionFactory = JsonConvert.DeserializeObject<ConnectionFactory>(connJson);
            }
            else
                connectionFactory = new ConnectionFactory();

            persistentConnection = new RabbitMQPersistentConnection(connectionFactory, eventBusConfig.ConnectionRetryCount);

            consumerChannel = CreateConsumerChannel();

            SubscriptionManager.OnEventRemoved += SubscriptionManager_OnEventRemoded;
        }

        private void SubscriptionManager_OnEventRemoded(object? sender, string eventName)
        {
            eventName = ProcessEventName(eventName);

            if(!persistentConnection.IsConnected)
                persistentConnection.TryConnect();

            consumerChannel.QueueUnbind(
                queue: eventName,
                exchange: EventBusConfig.DefaultTopicName,
                routingKey: eventName
                );

            if (SubscriptionManager.IsEmpty)
                consumerChannel.Close();
        }

        public override void Publish(IntegrationEvent @event)
        {
            if(!persistentConnection.IsConnected)
                persistentConnection.TryConnect();

            var policy = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(EventBusConfig.ConnectionRetryCount, retryAttemt => TimeSpan.FromSeconds(Math.Pow(2, retryAttemt)), (ex, time) => {
                    //log
                });

            var eventName = @event.GetType().Name;
            eventName = ProcessEventName(eventName);

            consumerChannel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");

            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            policy.Execute(() => { 
                var properties = consumerChannel.CreateBasicProperties();
                properties.DeliveryMode = 2;

                consumerChannel.QueueDeclare(
                    queue: GetSubName(eventName),
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                    );

                consumerChannel.BasicPublish(
                    exchange: EventBusConfig.DefaultTopicName,
                    routingKey: eventName,
                    mandatory: true,
                    basicProperties: properties,
                    body: body
                    );
            });
        }

        public override void Subscribe<T, TH>()
        {
            var eventName = typeof(T).Name;
            eventName = ProcessEventName(eventName);

            if (!SubscriptionManager.HashSubscriptionForEvent(eventName))
            {
                if (!persistentConnection.IsConnected)
                    persistentConnection.TryConnect();

                consumerChannel.QueueDeclare(
                    queue: GetSubName(eventName),
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                    );

                consumerChannel.QueueBind(
                    queue: GetSubName(eventName),
                    exchange: EventBusConfig.DefaultTopicName,
                    routingKey: eventName
                    );
            }

            SubscriptionManager.AddSubscription<T, TH>();
            StartBasicConsume(eventName);
        }

        public override void UnSubscribe<T, TH>()
        {
            SubscriptionManager.RemoveSubscription<T, TH>();
        }

        private IModel CreateConsumerChannel()
        {
            if (!persistentConnection.IsConnected)
                persistentConnection.TryConnect();

            var channel = persistentConnection.CreateModel();

            channel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");

            return channel;
        }

        private void StartBasicConsume(string eventName)
        {
            if (consumerChannel != null)
            {
                var consumer = new EventingBasicConsumer(consumerChannel);

                consumer.Received += Consumer_Received;

                consumerChannel.BasicConsume(
                    queue: GetSubName(eventName),
                    autoAck: false,
                    consumer: consumer
                    );
            }
        }

        private async void Consumer_Received(object? sender, BasicDeliverEventArgs e)
        {
            var eventName = e.RoutingKey;
            eventName = ProcessEventName(eventName);
            var message = Encoding.UTF8.GetString(e.Body.Span);

            try
            {
                await ProcessEvent(eventName, message);
            }
            catch (Exception ex)
            {
                //log
            }

            consumerChannel.BasicAck(e.DeliveryTag, multiple: false);
        }
    }
}
