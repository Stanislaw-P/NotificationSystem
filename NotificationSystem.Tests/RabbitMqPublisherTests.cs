using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationService.Worker.Options;
using NotificationService.Worker.Services;
using OrderService.Api.Services;
using RabbitMQ.Client;
using Shared.Contracts;

namespace NotificationSystem.Tests
{
    public class EmailSenderTests
    {
        private EmailSender CreateSender(string host = "localhost", int port = 25)
        {
            var options = Options.Create(new SmtpOptions
            {
                Host = host,
                Port = port,
            });

            return new EmailSender(options, NullLogger<EmailSender>.Instance);
        }

        [Fact]
        public async Task SendEmailAsync_ShouldThrow_WhenSmtpUnreachable()
        {
            // Arrange Ч несуществующий SMTP хост
            var sender = CreateSender(host: "nonexistent-host-12345", port: 9999);

            // Act
            var act = async () => await sender.SendEmailAsync(
                toEmail: "recipient@test.com",
                subject: "Test",
                body: "Test body");

            // Assert Ч при недоступном SMTP должно выброситьс€ исключение
            // (а не проглотитьс€), чтобы консьюмер мог сделать nack
            await act.Should().ThrowAsync<Exception>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-email")]
        public async Task SendEmailAsync_ShouldThrow_WhenEmailInvalid(string invalidEmail)
        {
            // Arrange
            var sender = CreateSender();

            // Act
            var act = async () => await sender.SendEmailAsync(
                toEmail: invalidEmail,
                subject: "Test",
                body: "Test body");

            // Assert Ч невалидный email должен давать исключение до SMTP
            await act.Should().ThrowAsync<Exception>();
        }
    }
    public class RabbitMqPublisherTests
    {
        private readonly Mock<IConnection> _connectionMock;
        private readonly Mock<IChannel> _channelMock;
        private readonly RabbitMqPublisher _publisher;

        public RabbitMqPublisherTests()
        {
            _channelMock = new Mock<IChannel>();
            _connectionMock = new Mock<IConnection>();

            // Ќастраиваем: при CreateChannelAsync возвращаем мок канала
            _connectionMock
                .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_channelMock.Object);

            _publisher = new RabbitMqPublisher(_connectionMock.Object);
        }

        [Fact]
        public async Task PublishOrderCreatedAsync_ShouldCallBasicPublish_WithCorrectRoutingKey()
        {
            // Arrange
            var orderEvent = new OrderCreatedEvent
            {
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@test.com",
                Amount = 100m,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Act
            await _publisher.PublishOrderCreatedAsync(orderEvent);

            // Assert Ч используем EmptyBasicProperty вместо BasicProperties
            _channelMock.Verify(c => c.BasicPublishAsync(
                It.Is<string>(exchange => exchange == string.Empty),
                It.Is<string>(rk => rk == "order-created-queue"),
                It.IsAny<bool>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishOrderCreatedAsync_ShouldSerializeEvent_ToJson()
        {
            // Arrange
            ReadOnlyMemory<byte> capturedBody = default;
            var orderId = Guid.NewGuid();

            var orderEvent = new OrderCreatedEvent
            {
                OrderId = orderId,
                CustomerEmail = "test@test.com",
                Amount = 250m,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // ѕерехватываем тело сообщени€ которое передаЄтс€ в BasicPublishAsync
            _channelMock
                .Setup(c => c.BasicPublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<BasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                    (_, _, _, _, body, _) => capturedBody = body)
                .Returns(ValueTask.CompletedTask);

            // Act
            await _publisher.PublishOrderCreatedAsync(orderEvent);

            // Assert Ч тело содержит правильный OrderId в JSON
            var json = System.Text.Encoding.UTF8.GetString(capturedBody.ToArray());
            json.Should().Contain(orderId.ToString());
            json.Should().Contain("test@test.com");
        }

        [Fact]
        public async Task PublishOrderCreatedAsync_ShouldCreateAndDisposeChannel()
        {
            // Arrange
            var orderEvent = new OrderCreatedEvent
            {
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@test.com",
                Amount = 100m,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Act
            await _publisher.PublishOrderCreatedAsync(orderEvent);

            // Assert Ч канал создаЄтс€ один раз на каждую публикацию
            _connectionMock.Verify(
                c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // » dispose вызываетс€ (using var channel)
            _channelMock.Verify(c => c.DisposeAsync(), Times.Once);
        }
    }
}