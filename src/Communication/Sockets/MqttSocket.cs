using DAT.Utils;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System.Net.Mime;

// TODO: check feasibility of IMessage, Message<T>
// TODO: check if blocking execution has any effect
// TODO: implement UseWorkQeue (at subscriptions)

namespace DAT.Communication {
  public class MqttSocket : ISocket {

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
    private IManagedMqttClient client;
    private CancellationTokenSource cts;

    private HashSet<SubscriptionOptions> subscriptions;
    private HashSet<SubscriptionOptions> pendingSubscriptions;
    private SubscriptionOptions defaultSubscriptionOptions;
    private PublicationOptions defaultPublicationOptions;
    private RequestOptions defaultRequestOptions;
    private bool blockingActionExecution;

    private Dictionary<SubscriptionOptions, List<ActionItem>> actions;
    private Dictionary<RequestOptions, TaskCompletionSource<IMessage>> promises;

    public MqttSocket(string id, string name, HostAddress address, IPayloadConverter converter, SubscriptionOptions defSubOptions = null, PublicationOptions defPubOptions = null, RequestOptions defReqOptions = null, bool blockingActionExecution = false) {
      this.id = id;
      this.name = name;
      this.address = address;
      this.converter = converter;

      cts = new CancellationTokenSource();
      client = new MqttFactory().CreateManagedMqttClient();

      subscriptions = new HashSet<SubscriptionOptions>();
      pendingSubscriptions = new HashSet<SubscriptionOptions>();
      actions = new Dictionary<SubscriptionOptions, List<ActionItem>>();
      promises = new Dictionary<RequestOptions, TaskCompletionSource<IMessage>>();

      this.defaultSubscriptionOptions = defSubOptions;
      this.defaultPublicationOptions = defPubOptions;
      this.defaultRequestOptions = defReqOptions;
      this.blockingActionExecution = blockingActionExecution;

      if (defSubOptions != null) pendingSubscriptions.Add(defSubOptions);

      client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
    }

    public object Clone() {
      return new MqttSocket(Id, Name, Address, Converter,
        (SubscriptionOptions)DefaultSubscriptionOptions?.Clone(),
        (PublicationOptions)DefaultPublicationOptions?.Clone(),
        (RequestOptions)DefaultRequestOptions?.Clone(),
        BlockingActionExecution);
    }

    private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg) {
      // parse received message
      Message msg = converter.Deserialize<Message>(arg.ApplicationMessage.Payload);
      

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
          if (Misc.CompareTopics(item.Key.ResponseTopic, msg.Topic)) {
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
              var imsg = CreateIMessage(msg, item.Item1.ContentType);
              item.Item2.Action(imsg, item.Item2.Token);
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
            var imsg = CreateIMessage(msg, item.Item1.ContentType);
            item.Item2.Action(imsg, item.Item2.Token);
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

      return t;
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
      if (IsConnected()) return true;

      var options = new MqttClientOptionsBuilder()
        .WithClientId(Name)
        .WithTcpServer(address.Server, address.Port);
      var mgOptions = new ManagedMqttClientOptionsBuilder()
        .WithClientOptions(options.Build())
        .Build();

      client.StartAsync(mgOptions).Wait(cts.Token);

      if (defaultSubscriptionOptions != null && defaultSubscriptionOptions.Topic != null)
        client.SubscribeAsync(defaultSubscriptionOptions.Topic, GetQosLevel(defaultSubscriptionOptions.QosLevel)).Wait(cts.Token);

      foreach (var subscription in pendingSubscriptions) {
        client.SubscribeAsync(subscription.Topic, GetQosLevel(subscription.QosLevel)).Wait(cts.Token);
      }
      pendingSubscriptions.Clear();

      return client.IsConnected;
    }

    public bool Disconnect() {
      cts.Cancel();
      client.StopAsync().Wait();
      client.Dispose();
      client = null;

      return IsConnected();
    }

    public void Abort() {
      cts.Cancel();
      client.StopAsync();
      client.Dispose();
      client = null;
    }

    public bool IsConnected() {
      return client != null && client.IsConnected;
    }

    public void Subscribe(SubscriptionOptions options) {
      var o = options != null ? options : DefaultSubscriptionOptions;

      if (IsConnected()) {
        subscriptions.Add(options);
        client.SubscribeAsync(options.Topic, GetQosLevel(options.QosLevel)).Wait(cts.Token);
      }
      else {
        pendingSubscriptions.Add(options);
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
      if (topic != null) {
        client.UnsubscribeAsync(topic).Wait(cts.Token);
        subscriptions.RemoveWhere(s => s.Topic == topic);
      }
      else {
        client.UnsubscribeAsync(subscriptions.Select(x => x.Topic).ToList()).Wait(cts.Token);
        subscriptions.Clear();
      }
    }

    public void Publish<T>(T payload, PublicationOptions options = null) {
      var task = PublishAsync(payload, options);
      task.Wait(cts.Token);
    }

    public async Task PublishAsync<T>(T payload, PublicationOptions options = null) {
      var o = options != null ? options : DefaultPublicationOptions;

      // setup message      
      var msg = new Message<T>(Id, Name, o.Topic, o.ResponseTopic, typeof(T).FullName, converter.Serialize<T>(payload), payload);

      var appMessage = new MqttApplicationMessageBuilder()
        .WithTopic(msg.Topic)
        .WithResponseTopic(msg.ResponseTopic)
        .WithPayload(converter.Serialize(msg))
        .WithQualityOfServiceLevel(GetQosLevel(o.QosLevel))
        .Build();

      var mappMessage = new ManagedMqttApplicationMessageBuilder()
        .WithApplicationMessage(appMessage)        
        .Build();

      await client.EnqueueAsync(mappMessage);
    }

    public T Request<T>(RequestOptions options = null) {
      if (!IsConnected()) throw new Exception("MqttSocket: Socket must be connected before a blocking request can be made.");

      return RequestAsync<T>(options).Result;
    }

    public async Task<T> RequestAsync<T>(RequestOptions options = null) {
      return await RequestAsync<T, object>(null, options);
    }

    public T1 Request<T1, T2>(T2 message, RequestOptions options = null) {
      if (!IsConnected()) throw new Exception("MqttSocket: Socket must be connected before a blocking request can be made.");

      return RequestAsync<T1, T2>(message, options).Result;
    }

    public async Task<T1> RequestAsync<T1, T2>(T2 payload, RequestOptions options = null) {
      // parse options
      var o = options != null ? options : (RequestOptions)DefaultRequestOptions.Clone();
      var rt = o.GenerateResponseTopicPostfix
        ? string.Concat(o.ResponseTopic, "/", Misc.GenerateId(10))
        : o.ResponseTopic;
      o.ResponseTopic = rt;


      // configure promise
      var promise = new TaskCompletionSource<IMessage>();
      promises.Add(o, promise);
      Subscribe(o.GetResponseSubscriptionOptions());

      // build request message
      var appMessageBuilder = new MqttApplicationMessageBuilder()
        .WithTopic(o.Topic)
        .WithResponseTopic(o.ResponseTopic)
        .WithUserProperty(Name, Name)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce);

      // setup message
      string contentType = payload != null ? typeof(T2).FullName : "";
      IMessage msg = new Message<T2>(Id, Name, o.Topic, o.ResponseTopic, contentType, payload);

      appMessageBuilder = msg != null
        ? appMessageBuilder.WithPayload(converter.Serialize(msg))
        : appMessageBuilder;

      var mappMessage = new ManagedMqttApplicationMessageBuilder()
        .WithApplicationMessage(appMessageBuilder.Build())        
        .Build();

      // send request message
      await client.EnqueueAsync(mappMessage);

      // await response message
      var response = await promise.Task;

      // deregister promise handling
      Unsubscribe(o.ResponseTopic);
      promises.Remove(o);

      // deserialize and return response
      return converter.Deserialize<T1>(response.Payload);
    }

    #region helper

    private MqttQualityOfServiceLevel GetQosLevel(QualityOfServiceLevel qosl) {
      if (qosl == QualityOfServiceLevel.AtMostOnce) return MqttQualityOfServiceLevel.AtMostOnce;
      else if (qosl == QualityOfServiceLevel.AtLeastOnce) return MqttQualityOfServiceLevel.AtLeastOnce;
      else return MqttQualityOfServiceLevel.ExactlyOnce;
    }

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
