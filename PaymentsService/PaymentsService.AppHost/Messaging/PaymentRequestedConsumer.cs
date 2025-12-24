using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class PaymentRequestedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionProvider _connProvider;
    private readonly RabbitMqOptions _opt;
    private IModel? _ch;
    public PaymentRequestedConsumer(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionProvider connProvider,
        IOptions<RabbitMqOptions> opt)
    {
        _scopeFactory = scopeFactory;
        _connProvider = connProvider;
        _opt = opt.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ch = _connProvider.CreateChannel();
        _ch.ExchangeDeclare(_opt.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        _ch.QueueDeclare(_opt.QueuePaymentRequested, durable: true, exclusive: false, autoDelete: false);
        _ch.QueueBind(_opt.QueuePaymentRequested, _opt.Exchange, _opt.RoutingPaymentRequested);
        _ch.BasicQos(0, 10, false); 
        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += OnReceivedAsync;
        _ch.BasicConsume(queue: _opt.QueuePaymentRequested, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
        PaymentRequested? msg;
        try
        {
            msg = JsonSerializer.Deserialize<PaymentRequested>(body);
            if (msg is null) throw new Exception("Empty message");
        }
        catch
        {
            _ch!.BasicAck(ea.DeliveryTag, multiple: false);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            var already = await db.InboxMessages.AnyAsync(x => x.Id == msg.MessageId);
            if (already)
            {
                await tx.CommitAsync();
                _ch!.BasicAck(ea.DeliveryTag, false);
                return;
            }
            db.InboxMessages.Add(new InboxMessage
            {
                Id = msg.MessageId,
                ReceivedAt = DateTimeOffset.UtcNow,
                EventType = "PaymentRequested"
            });
            var existsTx = await db.PaymentTransactions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == msg.OrderId);
            string status;
            string? reason;
            if (existsTx is not null)
            {
                status = existsTx.Result;
                reason = status == "Succeeded" ? null : "Already processed (failed)";
            }
            else
            {
                var acc = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == msg.UserId);
                if (acc is null)
                {
                    status = "Failed";
                    reason = "Account not found";
                }
                else
                {
                    var now = DateTimeOffset.UtcNow;
                    var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                        update accounts
                        set balance = balance - {msg.Amount},
                            version = version + 1,
                            updated_at = {now}
                        where user_id = {msg.UserId}
                          and balance >= {msg.Amount}
                          and version = {acc.Version};
                    ");
                    if (rows == 1)
                    {
                        status = "Succeeded";
                        reason = null;
                    }
                    else
                    {
                        var acc2 = await db.Accounts.AsNoTracking().FirstAsync(x => x.UserId == msg.UserId);
                        status = "Failed";
                        reason = acc2.Balance < msg.Amount ? "Insufficient funds" : "Concurrent update";
                    }
                }
                db.PaymentTransactions.Add(new PaymentTransaction
                {
                    OrderId = msg.OrderId,
                    UserId = msg.UserId,
                    Amount = msg.Amount,
                    Result = status,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            var resMsgId = Guid.NewGuid();
            var result = new PaymentResult(
                resMsgId,
                msg.OrderId,
                status == "Succeeded" ? "Succeeded" : "Failed",
                reason,
                DateTimeOffset.UtcNow);
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = resMsgId,
                OccurredAt = result.OccurredAt,
                EventType = "PaymentResult",
                Exchange = _opt.Exchange,
                RoutingKey = _opt.RoutingPaymentResult,
                Payload = JsonSerializer.Serialize(result),
                PublishedAt = null,
                Attempts = 0
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            _ch!.BasicAck(ea.DeliveryTag, false);
        }
        catch
        {
            _ch!.BasicNack(ea.DeliveryTag, false, requeue: true);
        }
    }
    public override void Dispose()
    {
        _ch?.Dispose();
        base.Dispose();
    }
}
