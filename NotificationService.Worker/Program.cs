using NotificationService.Worker;
using NotificationService.Worker.Options;
using NotificationService.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var host = builder.Build();
host.Run();
