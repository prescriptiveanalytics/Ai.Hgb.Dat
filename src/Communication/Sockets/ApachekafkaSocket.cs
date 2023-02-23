using Confluent.Kafka;
using DCT.Utils;
using System.Net;
using System.Text.RegularExpressions;

namespace DCT.Communication {
  public class ApachekafkaSocket : ISocket {
    public HostAddress Address {
      get { return address; }
    }

    public IEnumerable<SubscriptionOptions> Subscriptions {
      get => subscriptions;
    }

    public SubscriptionOptions DefaultSubscriptionOptions {
      get => defaultSubscriptionOptions;
      set => defaultSubscriptionOptions = value;
    }

    public PublicationOptions DefaultPublicationOptions {
      get => defaultPublicationOptions;
      set => defaultPublicationOptions = value;
    }

    public RequestOptions DefaultRequestOptions {
      get => defaultRequestOptions;
      set => defaultRequestOptions = value;
    }

    public IPayloadConverter Converter {
      get => converter;
      set => converter = value;
    }

    public bool BlockingActionExecution {
      get => blockingActionExecution;
      set => blockingActionExecution = value;
    }

    public event EventHandler<EventArgs<IMessage>> MessageReceived_BeforeRegisteredHandlers;

    public event EventHandler<EventArgs<IMessage>> MessageReceived_AfterRegisteredHandlers;

    private HostAddress address;
    private IPayloadConverter converter;
    private IProducer<Null, byte[]> producer;
    private IConsumer<Null, byte[]> consumer;
    private CancellationTokenSource cts;

    private HashSet<SubscriptionOptions> subscriptions;
    private HashSet<SubscriptionOptions> pendingSubscriptions;
    private SubscriptionOptions defaultSubscriptionOptions;
    private PublicationOptions defaultPublicationOptions;
    private RequestOptions defaultRequestOptions;
    private bool blockingActionExecution;

    private Dictionary<SubscriptionOptions, List<ActionItem>> actions;
    private Dictionary<RequestOptions, TaskCompletionSource<IMessage>> promises;


    public ApachekafkaSocket(HostAddress address, IPayloadConverter converter, SubscriptionOptions defSubOptions = null, PublicationOptions defPubOptions = null, RequestOptions defReqOptions = null) {
      this.address = address;
      this.converter = converter;

      cts = new CancellationTokenSource();
      var pConfig = new ProducerConfig
      {
        BootstrapServers = address.Address,
        ClientId = Dns.GetHostName() + "_" + Misc.GenerateId(5)
      };
      var cConfig = new ConsumerConfig
      {
        BootstrapServers = address.Address,
        GroupId = Dns.GetHostName() + "_" + Misc.GenerateId(5),
        AutoOffsetReset = AutoOffsetReset.Earliest,
        AllowAutoCreateTopics = true
        //, SecurityProtocol = SecurityProtocol.Plaintext
      };
      producer = new ProducerBuilder<Null, byte[]>(pConfig).Build();
      consumer = new ConsumerBuilder<Null, byte[]>(cConfig).Build();
      

      subscriptions = new HashSet<SubscriptionOptions>();
      pendingSubscriptions = new HashSet<SubscriptionOptions>();
      actions = new Dictionary<SubscriptionOptions, List<ActionItem>>();
      promises = new Dictionary<RequestOptions, TaskCompletionSource<IMessage>>();

      this.defaultSubscriptionOptions = defSubOptions;
      this.defaultPublicationOptions = defPubOptions;
      this.defaultRequestOptions = defReqOptions;
      //this.blockingActionExecution = blockingActionExecution;

      if (defSubOptions != null) pendingSubscriptions.Add(defSubOptions);

      //client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
    }

    public object Clone() {
      return new ApachekafkaSocket(Address, Converter,
        (SubscriptionOptions)DefaultSubscriptionOptions.Clone(),
        (PublicationOptions)DefaultPublicationOptions.Clone(),
        (RequestOptions)DefaultRequestOptions.Clone());
    }

    private void OnMessageReceived_BeforeRegisteredHandlers(IMessage message) {
      var handler = MessageReceived_BeforeRegisteredHandlers;
      if (handler != null) handler(this, new EventArgs<IMessage>(message));
    }

    private void OnMessageReceived_AfterRegisteredHandlers(IMessage message) {
      var handler = MessageReceived_AfterRegisteredHandlers;
      if (handler != null) handler(this, new EventArgs<IMessage>(message));
    }

    public bool Connect() {
      throw new NotImplementedException();
    }

    public bool Disconnect() {
      throw new NotImplementedException();
    }

    public void Abort() {
      throw new NotImplementedException();
    }

    public bool IsConnected() {
      throw new NotImplementedException();
    }

    public void Publish<T>(T message, PublicationOptions options = null) {
      throw new NotImplementedException();
    }

    public Task PublishAsync<T>(T message, PublicationOptions options = null) {
      throw new NotImplementedException();
    }

    public T Request<T>(RequestOptions options = null) {
      throw new NotImplementedException();
    }

    public T1 Request<T1, T2>(T2 message, RequestOptions options = null) {
      throw new NotImplementedException();
    }

    public Task<T> RequestAsync<T>(RequestOptions options = null) {
      throw new NotImplementedException();
    }

    public Task<T1> RequestAsync<T1, T2>(T2 message, RequestOptions options = null) {
      throw new NotImplementedException();
    }

    public void Subscribe(SubscriptionOptions options) {
      throw new NotImplementedException();
    }

    public void Subscribe(Action<IMessage, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null) {
      throw new NotImplementedException();
    }

    public void Subscribe<T>(Action<IMessage, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null) {
      throw new NotImplementedException();
    }

    public void Unsubscribe(string topic = null) {
      throw new NotImplementedException();
    }
  }
}
