using RabbitMQ.Client;
using Shared.Contracts;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OrderService.Api.Services
{
    public class RabbitMqPublisher : IMessagePublisher
    {
        const string QueueName = "order-created-queue";
        readonly IConnection connection;

        public RabbitMqPublisher(IConnection connection)
        {
            this.connection = connection;
        }

        public async Task PublishOrderCreatedAsync(OrderCreatedEvent orderEvent)
        {
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            var json = JsonSerializer.Serialize(orderEvent);
            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                body: body);
        }
    }
}
