public class PaymentTransaction
{
    public Guid OrderId { get; set; }  
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Result { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}