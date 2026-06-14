namespace Shared.Contracts
{
    public class OrderCreatedEvent
    {
        public Guid OrderId { get; init; }
        public string CustomerEmail { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
