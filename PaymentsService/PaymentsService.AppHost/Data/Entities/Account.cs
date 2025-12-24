public class Account
{
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
