using Microsoft.EntityFrameworkCore;

namespace NotificationService.Worker.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
            : base(options) { }

        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NotificationLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderId).IsRequired();
                entity.Property(e => e.CustomerEmail).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

                // Индекс для быстрого поиска по OrderId
                entity.HasIndex(e => e.OrderId);
            });
        }
    }
}
