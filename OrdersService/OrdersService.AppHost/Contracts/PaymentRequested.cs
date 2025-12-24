public record PaymentRequested(Guid MessageId, Guid OrderId, Guid UserId, decimal Amount, DateTimeOffset OccurredAt);
