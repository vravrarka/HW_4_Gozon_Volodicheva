public class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "kpo.events";
    public string QueuePaymentRequested { get; set; } = "payments.payment_requested";
    public string QueuePaymentResult { get; set; } = "orders.payment_result";

    public string RoutingPaymentRequested { get; set; } = "payments.requested";
    public string RoutingPaymentResult { get; set; } = "payments.result";
}
