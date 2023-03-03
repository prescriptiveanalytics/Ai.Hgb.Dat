using Confluent.Kafka;
using DAT.Utils;
using System.Net;
using System.Xml.Linq;

namespace DAT.Communication {
  public class ApachekafkaSocket : ISocket {
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

    private string name;
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


    public ApachekafkaSocket(string name, HostAddress address, IPayloadConverter converter, SubscriptionOptions defSubOptions = null, PublicationOptions defPubOptions = null, RequestOptions defReqOptions = null, bool blockingActionExecution = false) {
      this.name = name;
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
      this.blockingActionExecution = blockingActionExecution;

      if (defSubOptions != null) pendingSubscriptions.Add(defSubOptions);


      //client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
      Task.Factory.StartNew(() => ReceiveMessages(cts.Token), cts.Token);
    }

    private void ReceiveMessages(CancellationToken token) {
      // TODO: currently QOS = AutoCommit
      try {
        while (!token.IsCancellationRequested) {
          var consumeResult = consumer.Consume(token); // cancellation throws exception

          var msg = new Message();
          msg.ClientName = Name;
          msg.Topic = consumeResult.Topic;
          msg.Payload = consumeResult.Message.Value;
          // TODO: add additional meta info

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
      return new ApachekafkaSocket(Name, Address, Converter,
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
