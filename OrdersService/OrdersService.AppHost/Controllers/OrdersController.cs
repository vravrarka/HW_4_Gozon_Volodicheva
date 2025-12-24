using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly IConfiguration _cfg;
    public OrdersController(OrdersDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }
    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> Create([FromBody] CreateOrderRequest req)
    {
        if (req.Amount <= 0) return BadRequest("Amount must be > 0");
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            Id = orderId,
            UserId = req.UserId,
            Amount = req.Amount,
            Status = "NEW",
            CreatedAt = now,
            UpdatedAt = now
        };
        var msgId = Guid.NewGuid();
        var ev = new PaymentRequested(msgId, orderId, req.UserId, req.Amount, now);
        var outbox = new OutboxMessage
        {
            Id = msgId,
            OccurredAt = now,
            EventType = "PaymentRequested",
            Exchange = _cfg["RabbitMq:Exchange"] ?? "kpo.events",
            RoutingKey = "payments.requested",
            Payload = JsonSerializer.Serialize(ev),
            PublishedAt = null,
            Attempts = 0
        };
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Orders.Add(order);
        _db.OutboxMessages.Add(outbox);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreatedAtAction(nameof(GetById), new { orderId }, new CreateOrderResponse(orderId, "NEW"));
    }
    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid orderId)
    {
        var o = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId);
        if (o is null) return NotFound();
        return new OrderDto(o.Id, o.UserId, o.Amount, o.Status);
    }
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> List([FromQuery] Guid? userId)
    {
        var q = _db.Orders.AsQueryable();
        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OrderDto(x.Id, x.UserId, x.Amount, x.Status))
            .ToListAsync();
        return list;
    }
}