using Microsoft.EntityFrameworkCore;
using NotificationService.Worker;
using NotificationService.Worker.Data;
using NotificationService.Worker.Options;
using NotificationService.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
