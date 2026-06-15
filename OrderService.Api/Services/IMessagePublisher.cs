using Shared.Contracts;

namespace OrderService.Api.Services
{
    public interface IMessagePublisher
    {
        Task PublishOrderCreatedAsync(OrderCreatedEvent orderEvent);
    }
}
