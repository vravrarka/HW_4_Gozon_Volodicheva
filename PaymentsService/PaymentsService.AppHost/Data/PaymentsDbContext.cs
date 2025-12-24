using Microsoft.EntityFrameworkCore;
public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>().ToTable("accounts").HasKey(x => x.UserId);
        b.Entity<InboxMessage>().ToTable("inbox_messages").HasKey(x => x.Id);
        b.Entity<PaymentTransaction>().ToTable("payment_transactions").HasKey(x => x.OrderId);
        b.Entity<OutboxMessage>().ToTable("outbox_messages").HasKey(x => x.Id);
    }
}
