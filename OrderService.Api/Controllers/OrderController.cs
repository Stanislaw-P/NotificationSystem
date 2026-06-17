using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Services;
using Shared.Contracts;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        readonly IMessagePublisher _publisher;
        readonly ILogger<OrderController> _logger;

        public OrderController(IMessagePublisher publisher, ILogger<OrderController> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrderAsync([FromBody] CreateOrderRequest request)
        {
            var orderEvent = new OrderCreatedEvent
            {
                OrderId = Guid.NewGuid(),
                CustomerEmail = request.CustomerEmail,
                Amount = request.Amount,
                CreatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                await _publisher.PublishOrderCreatedAsync(orderEvent);
                _logger.LogInformation("Published OrderCreatedEvent for OrderId: {OrderId}", orderEvent.OrderId);
                
                return Accepted(new { orderEvent.OrderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish OrderCreatedEvent for OrderId: {OrderId}", orderEvent.OrderId);
                return BadRequest(ex.Message);
            }
        }
    }

    public record CreateOrderRequest(string CustomerEmail, decimal Amount);
}
