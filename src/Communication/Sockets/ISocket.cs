namespace DCT.Communication {
  public interface ISocket {

    HostAddress Address { get; }

    IEnumerable<SubscriptionOptions> Subscriptions { get; }

    event EventHandler<EventArgs<Message>> MessageReceived_BeforeRegisteredHandlers;

    event EventHandler<EventArgs<Message>> MessageReceived_AfterRegisteredHandlers;

    SubscriptionOptions DefaultSubscriptionOptions { get; set; }

    PublicationOptions DefaultPublicationOptions { get; set; }

    RequestOptions DefaultRequestOptions { get; set; }

    IPayloadConverter Converter { get; set; }

    bool BlockingActionExecution { get; set; }
    
    bool Connect();

    bool Disconnect();

    void Abort();

    bool IsConnected();

    void Subscribe(SubscriptionOptions options);

    void Subscribe(Action<Message, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null);

    void Unsubscribe(string topic = null);

    void Publish<T>(T message, PublicationOptions options = null);

    Task PublishAsync<T>(T message, PublicationOptions options = null);

    T Request<T>(RequestOptions options = null);

    Task<T> RequestAsync<T>(RequestOptions options = null);

    T1 Request<T1, T2>(T2 message, RequestOptions options = null);

    Task<T1> RequestAsync<T1, T2>(T2 message, RequestOptions options = null);
  }
}