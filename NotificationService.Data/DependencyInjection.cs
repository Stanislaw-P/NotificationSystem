using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Data
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddNotificationData(
        this IServiceCollection services,
        string connectionString)
        {
            services.AddDbContext<NotificationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Scoped — новый экземпляр на каждый scope (запрос / операция)
            services.AddScoped<INotificationLogRepository, NotificationLogRepository>();

            return services;
        }
    }
}
