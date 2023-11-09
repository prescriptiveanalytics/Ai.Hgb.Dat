using DAT.Configuration;
using DAT.Utils;

namespace DAT.Communication {
  /// <summary>
  /// This interface describes the base functionality all socket implementations must provide. 
  /// </summary>
  /// <remarks>
  /// Implementations may be done with arbitrary messaging technologies, such as MQTT, Apache Kafak, etc.
  /// To cover the interface functionality, respective publicly available clients for these technologies are utilized, wrapped and extended.
  /// This C# interface is ported also to other programming languages in order to enable cross-language interoperability.
  /// </remarks>
  public interface ISocket : ICloneable, IDisposable {

    SocketConfiguration Configuration { get; }

    IEnumerable<SubscriptionOptions> Subscriptions { get; }

    event EventHandler<EventArgs<IMessage>> MessageReceived_BeforeRegisteredHandlers;

    event EventHandler<EventArgs<IMessage>> MessageReceived_AfterRegisteredHandlers;

    IPayloadConverter Converter { get; set; }

    InterfaceStore InterfaceStore { get; }

    bool BlockingActionExecution { get; set; }
    
    /// <summary>
    /// Connects an ISocket instance to a message broker.
    /// </summary>
    /// <returns>An ISocket typed instane (enabling builder pattern)</returns>
    ISocket Connect();

    ISocket Disconnect();

    void Abort();

    bool IsConnected();

    void Subscribe(Action<IMessage, CancellationToken> handler, CancellationToken? token = null);

    void Subscribe(string topic, Action<IMessage, CancellationToken> handler, CancellationToken? token = null);

    void Subscribe(SubscriptionOptions options, Action<IMessage, CancellationToken> handler, CancellationToken? token = null);

    void Subscribe<T>(Action<IMessage, CancellationToken> handler, CancellationToken? token = null);

    void Subscribe<T>(string topic, Action<IMessage, CancellationToken> handler, CancellationToken? token = null);

    void Subscribe<T>(SubscriptionOptions options, Action<IMessage, CancellationToken> handler, CancellationToken? token = null);

    void Unsubscribe(string topic = null);

    void Publish<T>(T payload);

    void Publish<T>(string topic, T payload);

    void Publish<T>(PublicationOptions options, T payload);

    Task PublishAsync<T>(T payload);

    Task PublishAsync<T>(string topic, T payload);

    Task PublishAsync<T>(PublicationOptions options, T payload);

    T Request<T>();

    T Request<T>(string topic, string responseTopic, bool generateResponseTopicPostfix = true);

    T Request<T>(RequestOptions options);

    Task<T> RequestAsync<T>();

    Task<T> RequestAsync<T>(string topic, string responseTopic, bool generateResponseTopicPostfix = true);

    Task<T> RequestAsync<T>(RequestOptions options);

    T1 Request<T1, T2>(T2 payload);

    T1 Request<T1, T2>(string topic, string responseTopic, bool generateResponseTopicPostfix, T2 payload);

    T1 Request<T1, T2>(RequestOptions options, T2 payload);

    Task<T1> RequestAsync<T1, T2>(T2 payload);

    Task<T1> RequestAsync<T1, T2>(string topic, string responseTopic, bool generateResponseTopicPostfix, T2 payload);

    Task<T1> RequestAsync<T1, T2>(RequestOptions options, T2 payload);
  }
}