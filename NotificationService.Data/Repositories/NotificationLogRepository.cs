using Microsoft.EntityFrameworkCore;
using NotificationService.Data.Models;

namespace NotificationService.Data.Repositories
{
    public class NotificationLogRepository : INotificationLogRepository
    {
        private readonly NotificationDbContext _db;

        public NotificationLogRepository(NotificationDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default)
        {
            await _db.NotificationLogs.AddAsync(log, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<NotificationLog?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return await _db.NotificationLogs
                .FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        }

        public async Task<IReadOnlyList<NotificationLog>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _db.NotificationLogs
                .OrderByDescending(x => x.ProcessedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
