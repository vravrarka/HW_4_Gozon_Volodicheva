public class InboxMessage
{
    public Guid Id { get; set; } 
    public DateTimeOffset ReceivedAt { get; set; }
    public string EventType { get; set; } = default!;
}
