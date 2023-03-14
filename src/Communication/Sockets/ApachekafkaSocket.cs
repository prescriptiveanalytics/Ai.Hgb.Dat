using Confluent.Kafka;
using DAT.Utils;
using System.Net;
using System.Xml.Linq;

namespace DAT.Communication {
  public class ApachekafkaSocket : ISocket {

    public string Id {
      get => id;
      set => id = value;
    }

    public string Name {
      get => name;
      set => name = value;
    }
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

    private string id;
    private string name;
    private HostAddress address;
    private IPayloadConverter converter;
    private IProducer<Null, byte[]> producer;
    private IConsumer<Null, byte[]> consumer;
    private ProducerConfig pConfig;
    private ConsumerConfig cConfig;
    private Task producerTask;
    private Task consumerTask;
    private CancellationTokenSource cts;

    private HashSet<SubscriptionOptions> subscriptions;
    private HashSet<SubscriptionOptions> pendingSubscriptions;
    private SubscriptionOptions defaultSubscriptionOptions;
    private PublicationOptions defaultPublicationOptions;
    private RequestOptions defaultRequestOptions;
    private bool blockingActionExecution;

    private Dictionary<SubscriptionOptions, List<ActionItem>> actions;
    private Dictionary<RequestOptions, TaskCompletionSource<IMessage>> promises;


    public ApachekafkaSocket(string id, string name, HostAddress address, IPayloadConverter converter, SubscriptionOptions defSubOptions = null, PublicationOptions defPubOptions = null, RequestOptions defReqOptions = null, bool blockingActionExecution = false, bool connect = true) {
      this.id = id;
      this.name = name;
      this.address = address;
      this.converter = converter;

      cts = new CancellationTokenSource();
      pConfig = new ProducerConfig
      {
        BootstrapServers = address.Address,
        ClientId = id,
        //ApiVersionRequest = false
      };
      cConfig = new ConsumerConfig
      {
        BootstrapServers = address.Address,
        GroupId = id,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        AllowAutoCreateTopics = true
        //, SecurityProtocol = SecurityProtocol.Plaintext
      };
      

      subscriptions = new HashSet<SubscriptionOptions>();
      pendingSubscriptions = new HashSet<SubscriptionOptions>();
      actions = new Dictionary<SubscriptionOptions, List<ActionItem>>();
      promises = new Dictionary<RequestOptions, TaskCompletionSource<IMessage>>();

      this.defaultSubscriptionOptions = defSubOptions;
      this.defaultPublicationOptions = defPubOptions;
      this.defaultRequestOptions = defReqOptions;
      this.blockingActionExecution = blockingActionExecution;

      if (defSubOptions != null) pendingSubscriptions.Add(defSubOptions);
            

      if (connect) Connect();
    }

    private void ReceiveMessages(CancellationToken token) {
      // TODO: currently QOS = AutoCommit
      try {
        while (!token.IsCancellationRequested) {
          var consumeResult = consumer.Consume(token); // cancellation throws exception

          // parse received message
          Message msg = converter.Deserialize<Message>(consumeResult.Message.Value);

          // fire message received event (before executing individually registered handlers)
          OnMessageReceived_BeforeRegisteredHandlers(msg);

          // collect actions and promises to be executed
          var actionList = new List<Tuple<SubscriptionOptions, ActionItem>>();
          var promiseList = new List<TaskCompletionSource<IMessage>>();

          lock (actions) {
            foreach (var item in actions) {
              if (Misc.CompareTopics(item.Key.Topic, msg.Topic)) {
                foreach (var ai in item.Value) actionList.Add(Tuple.Create(item.Key, ai));
              }
            }
          }

          lock (promises) {
            foreach (var item in promises) {
              if (Misc.CompareTopics(item.Key.Topic, msg.Topic)) {
                promiseList.Add(item.Value);
              }
            }
          }

          // execute collected actions and promises
          Task t;
          if (!blockingActionExecution) {
            // v1: async (intended socket behavior)
            t = Task.Factory.StartNew(() =>
            {
              foreach (var item in actionList) {
                if (!cts.IsCancellationRequested) {
                  IMessage iMsg = CreateIMessage(msg, item.Item1.ContentType);
                  item.Item2.Action(iMsg, item.Item2.Token);
                }
              }
              foreach (var item in promiseList) {
                if (!cts.IsCancellationRequested) {
                  item.TrySetResult(msg);
                }
              }

            }, cts.Token);
          }
          else {
            // v2: blocking (threadsafe behavior regarding processing order)
            foreach (var item in actionList) {
              if (!cts.IsCancellationRequested) {
                IMessage iMsg = CreateIMessage(msg, item.Item1.ContentType);
                item.Item2.Action(iMsg, item.Item2.Token);
              }
            }
            foreach (var item in promiseList) {
              if (!cts.IsCancellationRequested) {
                item.TrySetResult(msg);
              }
            }
            t = Task.CompletedTask;
          }

          // fire message received event (after executing individually registered handlers)
          OnMessageReceived_AfterRegisteredHandlers(msg);

        }
      }
      catch (Exception ex) {
        consumer.Close();
      }
    }

    public object Clone() {
      return new ApachekafkaSocket(Id, Name, Address, Converter,
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
      if(producer != null || consumer != null) return true;

      producer = new ProducerBuilder<Null, byte[]>(pConfig).Build();
      consumer = new ConsumerBuilder<Null, byte[]>(cConfig).Build();
      consumerTask = Task.Factory.StartNew(() => ReceiveMessages(cts.Token), cts.Token);

      return true;
    }

    public bool Disconnect() {      
      producer.Dispose();
      consumer.Close();      
      consumer.Dispose();

      return true;
    }

    public void Abort() {
      producer.AbortTransaction();
      cts.Cancel();
      Disconnect();
    }

    public bool IsConnected() {
      return producer != null || consumer != null;    
    }

    public void Publish<T>(T payload, PublicationOptions options = null) {
      var task = PublishAsync(payload, options);
      task.Wait(cts.Token);
    }

    public async Task PublishAsync<T>(T payload, PublicationOptions options = null) {
      var o = options != null ? options : DefaultPublicationOptions;

      // setup message      
      var msg = new Message<T>(Id, Name, o.Topic, o.ResponseTopic, typeof(T).FullName, converter.Serialize<T>(payload), payload);
      var kafkaMsg = new Message<Null, byte[]> { Value = converter.Serialize(msg) };

      var t = producer.ProduceAsync(msg.Topic, kafkaMsg);
      await t;
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
      var o = options != null ? options : DefaultSubscriptionOptions;

      if (IsConnected()) {
        subscriptions.Add(o);
        consumer.Subscribe(o.Topic);        
      }
      else {
        pendingSubscriptions.Add(o);
      }
    }

    public void Subscribe(Action<IMessage, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null) {
      var o = options != null ? options : DefaultSubscriptionOptions;

      if (!actions.ContainsKey(o)) actions.Add(o, new List<ActionItem>());
      CancellationToken tok = token.HasValue ? token.Value : cts.Token;
      actions[o].Add(new ActionItem(handler, tok));

      Subscribe(o);
    }

    public void Subscribe<T>(Action<IMessage, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null) {
      var o = options != null ? options : DefaultSubscriptionOptions; // use new or default options as base
      if (o.ContentType != typeof(T)) { // create new options if requested type does not match the base
        o = (SubscriptionOptions)o.Clone();
        o.ContentType = typeof(T);
      }

      if (!actions.ContainsKey(o)) actions.Add(o, new List<ActionItem>());
      CancellationToken tok = token.HasValue ? token.Value : cts.Token;
      actions[o].Add(new ActionItem(handler, tok));

      Subscribe(o);
    }

    public void Unsubscribe(string topic = null) {
      consumer.Unsubscribe(); // TODO
    }

    #region helper

    private IMessage CreateIMessage(Message msg, Type type) {
      if (type == null) {
        msg.Content = converter.Deserialize(msg.Payload);
        return msg;
      }
      else {
        Type message_genericTypeDef = typeof(Message<>);
        Type[] typeArgs = { type };
        var requestedType = message_genericTypeDef.MakeGenericType(typeArgs);
        var instance = (IMessage)Activator.CreateInstance(requestedType);

        instance.ClientId = msg.ClientId;
        instance.Topic = msg.Topic;
        instance.ResponseTopic = msg.ResponseTopic;
        instance.Payload = msg.Payload.ToArray();
        instance.ContentType = msg.ContentType;

        instance.Content = converter.Deserialize(msg.Payload, type);
        return instance;
      }
    }

    #endregion helper
  }
}
