using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly PaymentsDbContext _db;
    public AccountsController(PaymentsDbContext db)
    {
        _db = db;
    }
    public record CreateAccountRequest(Guid UserId);
    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequest req)
    {
        var now = DateTimeOffset.UtcNow;
        var exists = await _db.Accounts.AnyAsync(x => x.UserId == req.UserId);
        if (exists) return Conflict("Account already exists");
        _db.Accounts.Add(new Account
        {
            UserId = req.UserId,
            Balance = 0m,
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();
        return Created("", new AccountDto(req.UserId, 0m));
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AccountDto>> Get(Guid userId)
    {
        var acc = await _db.Accounts.FirstOrDefaultAsync(x => x.UserId == userId);
        if (acc is null) return NotFound();
        return new AccountDto(acc.UserId, acc.Balance);
    }

    [HttpPost("{userId:guid}/topup")]
    public async Task<ActionResult<AccountDto>> TopUp(Guid userId, [FromBody] TopUpRequest req)
    {
        if (req.Amount <= 0) return BadRequest("Amount must be > 0");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var acc = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            if (acc is null) return NotFound();
            var now = DateTimeOffset.UtcNow;
            var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                update accounts
                set balance = balance + {req.Amount},
                    version = version + 1,
                    updated_at = {now}
                where user_id = {userId}
                  and version = {acc.Version};
            ");
            if (rows == 1)
            {
                var updated = await _db.Accounts.AsNoTracking().FirstAsync(x => x.UserId == userId);
                return new AccountDto(updated.UserId, updated.Balance);
            }
        }
        return StatusCode(409, "Concurrent update, try again");
    }
}