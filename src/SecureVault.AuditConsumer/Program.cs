using SecureVault.AuditConsumer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<AuditConsumerWorker>();

var host = builder.Build();
await host.RunAsync();
