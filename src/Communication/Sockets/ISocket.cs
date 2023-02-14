namespace DCT.Communication {
  public interface ISocket {

    HostAddress Address { get; }

    IEnumerable<string> Subscriptions { get; }

    event EventHandler<EventArgs<Message>> MessageReceived;

    string BaseTopic { get; set; }

    QualityOfServiceLevel QOSLevel { get; set; }    

    IPayloadConverter Converter { get; set; }
    
    bool Connect();

    bool Disconnect();

    void Abort();

    bool IsConnected();

    void Subscribe(string topic);

    void Subscribe(Action<Message, CancellationToken> handler, CancellationToken? token = null, string? topic = null);

    void Unsubscribe(string? topic = null);

    void Publish<T>(T message, string? topic = null);

    Task PublishAsync<T>(T message, string? topic = null);

    T Request<T>(string? topic = null);

    Task<T> RequestAsync<T>(string? topic = null);

    T1 Request<T1, T2>(T2 message, string? topic = null);

    Task<T1> RequestAsync<T1, T2>(T2 message, string? topic = null);
  }
}