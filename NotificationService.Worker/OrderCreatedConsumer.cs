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

            // ќбъ€вл€ем очередь Ч параметры должны совпадать с тем,
            // что объ€вл€ет OrderService, иначе RabbitMQ вернЄт ошибку
            await _channel.QueueDeclareAsync(
                       queue: QueueName,
                       durable: true,
                       exclusive: false,
                       autoDelete: false,
                       cancellationToken: cancellationToken);

            // √оворим брокеру: присылай не больше 1 сообщени€ за раз.
            // —ледующее придЄт только после того, как мы отправим ack/nack на текущее.
            // Ѕез этого RabbitMQ может завалить воркер сотн€ми сообщений одновременно.
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

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
                        _logger.LogWarning("Received null or invalid message with delivery tag {DeliveryTag}", deliveryTag);

                        // requeue: false Ч битое сообщение обратно в очередь не кладЄм,
                        // иначе получим бесконечный цикл
                        await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                    }

                    _logger.LogInformation(
                        "Processing order {OrderId} for {Email}",
                        orderEvent!.OrderId,
                        orderEvent.CustomerEmail);

                    await _emailSender.SendEmailAsync(
                        toEmail: orderEvent.CustomerEmail,
                        subject: $"¬аш заказ #{orderEvent.OrderId} оформлен",
                        body: $"—пасибо за заказ!\n\n—умма: {orderEvent.Amount:C}\nƒата: {orderEvent.CreatedAt:g}",
                        cancellationToken: stoppingToken);

                    // ¬сЄ ок Ч говорим брокеру, что сообщение обработано и можно удал€ть
                    await _channel!.BasicAckAsync(deliveryTag, multiple: false);

                    _logger.LogInformation("Order {OrderId} processed successfully", orderEvent.OrderId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message with delivery tag {DeliveryTag}", deliveryTag);

                    // requeue: true Ч вернуть сообщение в очередь дл€ повторной попытки.
                    // ¬ продакшене здесь нужен счЄтчик retry + Dead Letter Queue,
                    // чтобы "отравленное" сообщение не зависло в цикле навечно
                    await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
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
    }
}
