using Microsoft.EntityFrameworkCore;
using NotificationService.Data.Models;

namespace NotificationService.Data
{
    public class NotificationDbContext : DbContext
    {
        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NotificationLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerEmail).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.HasIndex(e => e.OrderId);
            });
        }
    }
}
