namespace SecureVault.AuditConsumer;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string Exchange { get; set; } = "securevault.audit";
    public string QueueName { get; set; } = "securevault.audit.log";
}
