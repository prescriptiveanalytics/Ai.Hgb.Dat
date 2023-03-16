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
        ClientId = Dns.GetHostName() + "_" + Misc.GenerateId(10),
        //ClientId = id,
        //ApiVersionRequest = false
      };
      cConfig = new ConsumerConfig
      {        
        BootstrapServers = address.Address,        
        GroupId = "bar" + "_" + Misc.GenerateId(10),
        ClientId = Dns.GetHostName() + "_" + Misc.GenerateId(10),
        AutoOffsetReset = AutoOffsetReset.Earliest,
        AllowAutoCreateTopics = true,        
      //, SecurityProtocol = SecurityProtocol.Plaintext
    };
      cConfig.EnableAutoCommit = false;
      

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
      // TODO: currently QOS = ConsumeAtMostOnce
      try {
        while (!token.IsCancellationRequested) {
          var consumeResult = consumer.Consume(token); // cancellation throws exception

          try { consumer.Commit(consumeResult); }
          catch (KafkaException ex) { Console.WriteLine($"Commit error: {ex.Error.Reason}"); }
          //Console.WriteLine($"Offset = {consumeResult.Offset}");

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
      } finally {
        consumer.Close();
      }

      consumer.Close();
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

      if (defaultSubscriptionOptions != null && defaultSubscriptionOptions.Topic != null)
        consumer.Subscribe(defaultSubscriptionOptions.Topic);        

      foreach (var subscription in pendingSubscriptions) {
        consumer.Subscribe(subscription.Topic);        
      }
      pendingSubscriptions.Clear();

      return true;
    }

    public bool Disconnect() {
      cts.Cancel();

      producer.Flush();
      producer.Dispose();            
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

      var t = producer.ProduceAsync(msg.Topic, kafkaMsg, cts.Token);      
      await t;
    }

    public T Request<T>(RequestOptions options = null) {
      if (!IsConnected()) throw new Exception("ApachekafkaSocket: Socket must be connected before a blocking request can be made.");
            
      return RequestAsync<T>(options).Result;
    }

    public T1 Request<T1, T2>(T2 payload, RequestOptions options = null) {
      if (!IsConnected()) throw new Exception("ApachekafkaSocket: Socket must be connected before a blocking request can be made.");

      return RequestAsync<T1, T2>(payload, options).Result;
    }

    public async Task<T> RequestAsync<T>(RequestOptions options = null) {      
      return await RequestAsync<T, object>(null, options);
    }

    public async Task<T1> RequestAsync<T1, T2>(T2 payload, RequestOptions options = null) {
      var o = options != null ? options : DefaultRequestOptions;
      var rt = o.GenerateResponseTopicPostfix
        ? string.Concat(o.ResponseTopic, "/", Misc.GenerateId(10))
        : o.ResponseTopic;
      o.ResponseTopic = rt;

      // configure promise
      var promise = new TaskCompletionSource<IMessage>();
      promises.Add(o, promise);
      Subscribe(o.GetResponseSubscriptionOptions());

      // setup message      
      string contentType = payload != null ? typeof(T2).FullName : "";
      var msg = new Message<T2>(Id, Name, o.Topic, o.ResponseTopic, contentType, payload);
      var kafkaMsg = new Message<Null, byte[]> { Value = converter.Serialize(msg) };

      // send request message
      await producer.ProduceAsync(msg.Topic, kafkaMsg, cts.Token);

      // await response message
      var response = await promise.Task;

      // deregister promise handling
      Unsubscribe(o.ResponseTopic);
      promises.Remove(o);

      // deserialize and return response
      return converter.Deserialize<T1>(response.Payload);
    }

    public void Subscribe(SubscriptionOptions options) {
      if (options == null) return;

      if (IsConnected()) {
        subscriptions.Add(options);
        consumer.Subscribe(subscriptions.Select(x => x.Topic));        
      }
      else {
        pendingSubscriptions.Add(options);
      }
    }

    public void Subscribe(Action<IMessage, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null) {
      var o = options != null ? options : DefaultSubscriptionOptions;

      if (!actions.ContainsKey(o)) {
        lock (actions) {
          actions.Add(o, new List<ActionItem>());
          CancellationToken tok = token.HasValue ? token.Value : cts.Token;
          actions[o].Add(new ActionItem(handler, tok));
        }
      }

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
      subscriptions.RemoveWhere(s => s.Topic == topic);
      consumer.Subscribe(subscriptions.Select(x => x.Topic));
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
