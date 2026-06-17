using Microsoft.Extensions.Options;
using NotificationService.Worker.Options;
using NotificationService.Worker.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using System.Text;
using System.Text.Json;

namespace NotificationService.Worker
{
    public class OrderCreatedConsumer : BackgroundService
    {
        private const string QueueName = "order-created-queue";
        private const string DeadLetterQueueName = "order-created-dlq";
        private const string DeadLetterExchangeName = "order-created-dlx";
        private const int MaxRetryCount = 3;

        private readonly ILogger<OrderCreatedConsumer> _logger;
        private readonly IEmailSender _emailSender;
        private readonly RabbitMqOptions _rabbitMQOptions;

        private IConnection? _connection;
        private IChannel? _channel;

        public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger, IEmailSender emailSender, IOptions<RabbitMqOptions> rabbitMQOptions)
        {
            _logger = logger;
            _emailSender = emailSender;
            _rabbitMQOptions = rabbitMQOptions.Value;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMQOptions.HostName,
                UserName = _rabbitMQOptions.UserName,
                Password = _rabbitMQOptions.Password
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // 1. ќбъ€вл€ем Dead Letter Exchange (тип direct Ч простой роутинг)
            await _channel.ExchangeDeclareAsync(
                exchange: DeadLetterExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                cancellationToken: cancellationToken);

            // 2. ќбъ€вл€ем Dead Letter Queue
            await _channel.QueueDeclareAsync(
                queue: DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // 3. Ѕиндим DLQ к DLX
            await _channel.QueueBindAsync(
                queue: DeadLetterQueueName,
                exchange: DeadLetterExchangeName,
                routingKey: DeadLetterQueueName,
                cancellationToken: cancellationToken);

            // 4. ќбъ€вл€ем основную очередь с указанием DLX
            // “еперь при nack(requeue:false) сообщение автоматически
            // улетит в DeadLetterExchangeName -> DeadLetterQueueName
            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
            { "x-dead-letter-exchange", DeadLetterExchangeName },
            { "x-dead-letter-routing-key", DeadLetterQueueName }
                },
                cancellationToken: cancellationToken);

            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: cancellationToken);

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel!);

            consumer.ReceivedAsync += async (sender, args) =>
            {
                var deliveryTag = args.DeliveryTag;

                try
                {
                    var body = args.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(json);

                    if (orderEvent is null)
                    {
                        _logger.LogWarning("Malformed message, sending to DLQ immediately");
                        await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    _logger.LogInformation(
                        "Processing order {OrderId} for {Email}, attempt #{Attempt}",
                        orderEvent.OrderId,
                        orderEvent.CustomerEmail,
                        GetRetryCount(args.BasicProperties) + 1);

                    await _emailSender.SendEmailAsync(
                        toEmail: orderEvent.CustomerEmail,
                        subject: $"¬аш заказ #{orderEvent.OrderId} оформлен",
                        body: $"—пасибо за заказ!\n\n—умма: {orderEvent.Amount:C}\nƒата: {orderEvent.CreatedAt:g}",
                        cancellationToken: stoppingToken);

                    await _channel!.BasicAckAsync(deliveryTag, multiple: false);

                    _logger.LogInformation("Order {OrderId} processed successfully", orderEvent.OrderId);
                }
                catch (Exception ex)
                {
                    var retryCount = GetRetryCount(args.BasicProperties);

                    if (retryCount >= MaxRetryCount)
                    {
                        // »счерпали попытки Ч отправл€ем в DLQ без повтора
                        _logger.LogError(ex,
                            "Order processing failed after {MaxRetry} attempts, sending to DLQ. DeliveryTag: {Tag}",
                            MaxRetryCount,
                            deliveryTag);

                        await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                    }
                    else
                    {
                        // ≈щЄ есть попытки Ч возвращаем в очередь
                        _logger.LogWarning(ex,
                            "Order processing failed, attempt {Attempt}/{MaxRetry}. Will retry. DeliveryTag: {Tag}",
                            retryCount + 1,
                            MaxRetryCount,
                            deliveryTag);

                        await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
                    }
                }
            };

            await _channel!.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false, // об€зательно false Ч подтверждаем вручную через Ack/Nack
            consumer: consumer,
            cancellationToken: stoppingToken);

            // ƒержим воркер живым до сигнала остановки
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        // ¬ызываетс€ при остановке хоста Ч закрываем соединение корректно
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null)
                await _channel.CloseAsync();

            if (_connection is not null)
                await _connection.CloseAsync();

            await base.StopAsync(cancellationToken);
        }

        private static int GetRetryCount(IReadOnlyBasicProperties properties)
        {
            // x-death по€вл€етс€ только после первого nack,
            // при первой попытке его нет вообще
            if (properties.Headers is null ||
                !properties.Headers.TryGetValue("x-death", out var xDeath))
                return 0;

            // x-death Ч это List<object>, каждый элемент Ч Dictionary с пол€ми
            // "count" (сколько раз), "queue", "reason" и др.
            if (xDeath is List<object> deaths && deaths.Count > 0)
            {
                var firstDeath = deaths[0] as Dictionary<string, object>;
                if (firstDeath is not null &&
                    firstDeath.TryGetValue("count", out var count))
                {
                    return Convert.ToInt32(count);
                }
            }

            return 0;
        }
    }
}
