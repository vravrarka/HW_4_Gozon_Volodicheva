using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionProvider _connProvider;
    private readonly RabbitMqOptions _opt;
    public OutboxPublisher(IServiceScopeFactory scopeFactory, RabbitMqConnectionProvider connProvider, IOptions<RabbitMqOptions> opt)
    {
        _scopeFactory = scopeFactory;
        _connProvider = connProvider;
        _opt = opt.Value;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var ch = _connProvider.CreateChannel();
        ch.ExchangeDeclare(_opt.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        var props = ch.CreateBasicProperties();
        props.DeliveryMode = 2; 
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                var batch = await db.OutboxMessages
                    .Where(x => x.PublishedAt == null)
                    .OrderBy(x => x.OccurredAt)
                    .Take(50)
                    .ToListAsync(stoppingToken);
                foreach (var m in batch)
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(m.Payload);
                        props.MessageId = m.Id.ToString();
                        ch.BasicPublish(exchange: m.Exchange, routingKey: m.RoutingKey, basicProperties: props, body: bytes);
                        m.PublishedAt = DateTimeOffset.UtcNow;
                        m.Attempts += 1;
                        m.LastError = null;
                    }
                    catch (Exception ex)
                    {
                        m.Attempts += 1;
                        m.LastError = ex.Message;
                    }
                }
                if (batch.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch {}
            await Task.Delay(1000, stoppingToken);
        }
    }
}
