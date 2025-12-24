using Microsoft.Extensions.Options;
using RabbitMQ.Client;

public class RabbitMqConnectionProvider : IDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _conn;
    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> opt)
    {
        var o = opt.Value;
        _factory = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.Username,
            Password = o.Password,
            DispatchConsumersAsync = true
        };
    }
    public IModel CreateChannel()
    {
        _conn ??= _factory.CreateConnection();
        return _conn.CreateModel();
    }
    public void Dispose() => _conn?.Dispose();
}

