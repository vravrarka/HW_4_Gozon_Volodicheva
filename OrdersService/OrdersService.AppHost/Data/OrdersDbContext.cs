using Microsoft.EntityFrameworkCore;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>().ToTable("orders").HasKey(x => x.Id);
        b.Entity<OutboxMessage>().ToTable("outbox_messages").HasKey(x => x.Id);
        b.Entity<InboxMessage>().ToTable("inbox_messages").HasKey(x => x.Id);

        b.Entity<OutboxMessage>()
            .HasIndex(x => x.PublishedAt);
    }
}
