using NotificationService.Data.Models;

namespace NotificationService.Data.Repositories
{
    public interface INotificationLogRepository
    {
        Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NotificationLog>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<NotificationLog?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    }
}