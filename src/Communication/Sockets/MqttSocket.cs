using Confluent.Kafka;
using DAT.Configuration;
using DAT.Utils;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Internal;
using MQTTnet.Protocol;
using System;
using System.Net.Mime;

namespace DAT.Communication {
  public class MqttSocket : ISocket {

    public SocketConfiguration Configuration {      
      get => configuration;
    }

    public IEnumerable<SubscriptionOptions> Subscriptions {
      get => subscriptions;
    }

    public IPayloadConverter Converter {
      get => converter;
      set => converter = value;
    }

    public InterfaceStore InterfaceStore {
      get => interfaceStore;
    }

    public bool BlockingActionExecution {
      get => blockingActionExecution;
      set => blockingActionExecution = value;
    }

    public event EventHandler<EventArgs<IMessage>> MessageReceived_BeforeRegisteredHandlers;

    public event EventHandler<EventArgs<IMessage>> MessageReceived_AfterRegisteredHandlers;

    private SocketConfiguration configuration;

    private IPayloadConverter converter;
    private IManagedMqttClient client;
    InterfaceStore interfaceStore;
    private CancellationTokenSource cts;
    private AutoResetEvent connected;
    private AutoResetEvent disconnected;
    private AutoResetEvent connectionChanged;
    private object locker;

    private HashSet<SubscriptionOptions> subscriptions;
    private HashSet<SubscriptionOptions> pendingSubscriptions;
    private bool blockingActionExecution;

    private Dictionary<SubscriptionOptions, List<ActionItem>> actions;
    private Dictionary<RequestOptions, TaskCompletionSource<IMessage>> promises;
    

    public MqttSocket(SocketConfiguration configuration) {
      this.configuration = configuration;
      this.configuration.ConfigurationChanged += Configuration_ConfigurationChanged; // react to config changes

      if (configuration.PayloadType == "json") converter = new JsonPayloadConverter();
      else if (configuration.PayloadType == "yaml") converter = new YamlPayloadConverter();
      else if (configuration.PayloadType == "memp") converter = new MemoryPackPayloadConverter();

      //this.blockingActionExecution = blockingActionExecution;

      locker = new object();
      cts = new CancellationTokenSource();
      connected = new AutoResetEvent(false);
      disconnected = new AutoResetEvent(false);
      connectionChanged = new AutoResetEvent(false);
      client = new MqttFactory().CreateManagedMqttClient();

      subscriptions = new HashSet<SubscriptionOptions>();
      pendingSubscriptions = new HashSet<SubscriptionOptions>();
      actions = new Dictionary<SubscriptionOptions, List<ActionItem>>();
      promises = new Dictionary<RequestOptions, TaskCompletionSource<IMessage>>();

      if (!string.IsNullOrWhiteSpace(configuration.DefaultSubscriptionOptions.Topic)) pendingSubscriptions.Add(configuration.DefaultSubscriptionOptions);

      client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
      client.ConnectedAsync += Client_ConnectedAsync;
      client.DisconnectedAsync += Client_DisconnectedAsync;
      client.ConnectingFailedAsync += Client_ConnectingFailedAsync;

      interfaceStore = new InterfaceStore(configuration.Id);

      //if (connect) Connect();

    }

    public MqttSocket(string id, string name, HostAddress address, IPayloadConverter converter, SubscriptionOptions defSubOptions = null, PublicationOptions defPubOptions = null, RequestOptions defReqOptions = null, bool blockingActionExecution = false, bool connect = true) {
      configuration = new SocketConfiguration();
      configuration.Id = id;
      configuration.Name = name;
      configuration.Broker = address;

      this.converter = converter;

      locker = new object();
      cts = new CancellationTokenSource();
      connected = new AutoResetEvent(false);
      disconnected = new AutoResetEvent(false);
      connectionChanged= new AutoResetEvent(false);
      client = new MqttFactory().CreateManagedMqttClient();      

      subscriptions = new HashSet<SubscriptionOptions>();
      pendingSubscriptions = new HashSet<SubscriptionOptions>();
      actions = new Dictionary<SubscriptionOptions, List<ActionItem>>();
      promises = new Dictionary<RequestOptions, TaskCompletionSource<IMessage>>();

      if (defSubOptions != null) configuration.DefaultSubscriptionOptions = defSubOptions;
      if (defPubOptions != null) configuration.DefaultPublicationOptions = defPubOptions;
      if (defReqOptions != null) configuration.DefaultRequestOptions = defReqOptions;
      this.blockingActionExecution = blockingActionExecution;

      if (!string.IsNullOrWhiteSpace(configuration.DefaultSubscriptionOptions.Topic)) pendingSubscriptions.Add(configuration.DefaultSubscriptionOptions);

      client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
      client.ConnectedAsync += Client_ConnectedAsync;
      client.DisconnectedAsync += Client_DisconnectedAsync;
      client.ConnectingFailedAsync += Client_ConnectingFailedAsync;
      client.ConnectionStateChangedAsync += Client_ConnectionStateChangedAsync;

      interfaceStore = new InterfaceStore(configuration.Id);

      if (connect) Connect();
    }


    public object Clone() {
      return new MqttSocket(Configuration.Id, Configuration.Name, Configuration.Broker, Converter,
        (SubscriptionOptions)Configuration.DefaultSubscriptionOptions?.Clone(),
        (PublicationOptions)Configuration.DefaultPublicationOptions?.Clone(),
        (RequestOptions)Configuration.DefaultRequestOptions?.Clone(),
        BlockingActionExecution);
    }

    private void Configuration_ConfigurationChanged(object sender, EventArgs<DAT.Configuration.IConfiguration> e) {      
      Console.WriteLine("Udating socket now...");
      var newConfiguration = e.Value as SocketConfiguration;

      // TODO

      configuration = newConfiguration;
    }

    private Task Client_ConnectedAsync(MqttClientConnectedEventArgs arg) {      
      connected.Set();
      return Task.CompletedTask;
    }

    private Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg) {
      disconnected.Set();
      return Task.CompletedTask;
    }

    private Task Client_ConnectingFailedAsync(ConnectingFailedEventArgs arg) {
      if(arg.ConnectResult == null) Console.WriteLine("Connecting failed.");
      return Task.CompletedTask;
    }

    private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg) {
      // MemoryPackWorkaround:
      // - read topic from arg, not from msg anymore: if arg topic is modified intermediately, this could lead to unwanted side effects
      // - deserialize always with type; if type is null, deserialization fails

      var actionList = new List<Tuple<SubscriptionOptions, ActionItem>>();
      var promiseList = new List<Tuple<RequestOptions, TaskCompletionSource<IMessage>>>();
      Type genericType = null;

      lock (actions) {
        foreach (var item in actions) {
          if (Misc.CompareTopics(item.Key.Topic, arg.ApplicationMessage.Topic)) {
            genericType = item.Key.ContentType;
            foreach (var ai in item.Value) actionList.Add(Tuple.Create(item.Key, ai));
          }
        }
      }

      lock (promises) {
        foreach (var item in promises) {
          if (Misc.CompareTopics(item.Key.ResponseTopic, arg.ApplicationMessage.Topic)) {
            genericType = item.Key.ContentType;
            promiseList.Add(Tuple.Create(item.Key, item.Value));
          }
        }
      }



      // parse received message V1
      var msg = converter.Deserialize<Message>(arg.ApplicationMessage.PayloadSegment.Array);
      var imsg = CreateIMessage(msg, genericType);

      // parse received message V2
      //Type message_genericTypeDef = typeof(Message<>);
      //Type[] typeArgs = { genericType };
      //var requestedType = message_genericTypeDef.MakeGenericType(typeArgs);
      //var imsg = (IMessage)converter.Deserialize(arg.ApplicationMessage.PayloadSegment.Array, requestedType);

      // ISM workaround:
      //var param = arg.ApplicationMessage.PayloadSegment.Array;
      //IMessage imsg = converter.Deserialize<Message<DoubleTypedArray>>(param);

      // backlog:
      //var genericMethod = converter.GetType().GetMethods().FirstOrDefault(
      //  x => x.Name.Equals("Deserialize", StringComparison.OrdinalIgnoreCase) &&
      //  x.IsGenericMethod && x.GetParameters().Length == 1)
      //  ?.MakeGenericMethod(genericType);
      //dynamic result = genericMethod.Invoke(converter, new object[] { param });
      //var imsg = (IMessage)result;  

      // fire message received event (before executing individually registered handlers)
      OnMessageReceived_BeforeRegisteredHandlers(imsg);

      // collect actions and promises to be executed
      //var actionList = new List<Tuple<SubscriptionOptions, ActionItem>>();
      //var promiseList = new List<Tuple<RequestOptions, TaskCompletionSource<IMessage>>>();

      //lock (actions) {
      //  foreach (var item in actions) {
      //    if (Misc.CompareTopics(item.Key.Topic, msg.Topic)) {
      //      foreach (var ai in item.Value) actionList.Add(Tuple.Create(item.Key, ai));
      //    }
      //  }
      //}

      //lock (promises) {
      //  foreach (var item in promises) {
      //    if (Misc.CompareTopics(item.Key.ResponseTopic, msg.Topic)) {
      //      promiseList.Add(Tuple.Create(item.Key, item.Value));
      //    }
      //  }
      //}

      // execute collected actions and promises
      Task t;
      if (!blockingActionExecution) {
        // v1: async (intended socket behavior)
        t = Task.Factory.StartNew(() =>
        {
          foreach (var item in actionList) {
            if (!cts.IsCancellationRequested) {
              //var imsg = CreateIMessage(msg, item.Item1.ContentType);
              item.Item2.Action(imsg, item.Item2.Token);
            }
          }
          foreach (var item in promiseList) {
            if (!cts.IsCancellationRequested) {                            
              item.Item2.TrySetResult(imsg);
              //item.Item2.TrySetResult(msg);
            }
          }

        }, cts.Token);
      }
      else {
        // v2: blocking (threadsafe behavior regarding processing order)
        foreach (var item in actionList) {
          if (!cts.IsCancellationRequested) {
            //var imsg = CreateIMessage(msg, item.Item1.ContentType);
            item.Item2.Action(imsg, item.Item2.Token);
          }
        }
        foreach (var item in promiseList) {
          if (!cts.IsCancellationRequested) {
            item.Item2.TrySetResult(imsg);
            //item.Item2.TrySetResult(msg);
          }
        }
        t = Task.CompletedTask;
      }

      // fire message received event (after executing individually registered handlers)
      OnMessageReceived_AfterRegisteredHandlers(imsg);

      return t;
    }

    private Task Client_ConnectionStateChangedAsync(EventArgs arg) {
      connectionChanged.Set();

      return Task.CompletedTask;
    }

    private void OnMessageReceived_BeforeRegisteredHandlers(IMessage message) {
      var handler = MessageReceived_BeforeRegisteredHandlers;
      if (handler != null) handler(this, new EventArgs<IMessage>(message));
    }

    private void OnMessageReceived_AfterRegisteredHandlers(IMessage message) {
      var handler = MessageReceived_AfterRegisteredHandlers;
      if (handler != null) handler(this, new EventArgs<IMessage>(message));
    }

    public ISocket Connect() {
      if (IsConnected()) return this;

      var options = new MqttClientOptionsBuilder()
        .WithClientId(Configuration.Name)
        .WithWebSocketServer("ws://127.0.0.1:5000/mqtt")         
        .WithTcpServer(configuration.Broker.Name, configuration.Broker.Port)
        .WithCleanSession(true);
      var mgOptions = new ManagedMqttClientOptionsBuilder()
        .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
        .WithClientOptions(options.Build())
        .Build();

      client.StartAsync(mgOptions).Wait(cts.Token);
      client.ConnectingFailedAsync += OnClient_ConnectingFailedAsync;
      connected.WaitOne();

      if (configuration.DefaultSubscriptionOptions != null && configuration.DefaultSubscriptionOptions.Topic != null)
        client.SubscribeAsync(configuration.DefaultSubscriptionOptions.Topic, GetQosLevel(configuration.DefaultSubscriptionOptions.QosLevel)).Wait(cts.Token);

      foreach (var subscription in pendingSubscriptions) {
        client.SubscribeAsync(subscription.Topic, GetQosLevel(subscription.QosLevel)).Wait(cts.Token);
      }
      pendingSubscriptions.Clear();

      return this;
    }

    private Task OnClient_ConnectingFailedAsync(ConnectingFailedEventArgs arg) {
      Console.WriteLine(arg.Exception.InnerException.ToString());
      return Task.CompletedTask;
    }

    public ISocket Disconnect() {
      client.StopAsync().Wait(cts.Token);
      cts.Cancel();      
      disconnected.WaitOne();
      client.Dispose();
      client = null;

      return this;
    }

    public void Abort() {
      cts.Cancel();
      client.StopAsync();
      client.Dispose();
      client = null;
    }

    public void Dispose() {
      Disconnect();
    }

    public bool IsConnected() {
      return client != null && client.IsConnected;
    }

    private void Subscribe(SubscriptionOptions options) {
      var o = options != null ? options : configuration.DefaultSubscriptionOptions;

      if (IsConnected()) {
        subscriptions.Add(o);

        // V0 ... buggy
        //client.SubscribeAsync(o.Topic, GetQosLevel(o.QosLevel)).Wait(cts.Token);        
        //client.SubscribeAsync(o.Topic).Wait(cts.Token);
        //connectionChanged.WaitOne();

        // V1 ... infeasible (ensure context switch, i.e. subscription, due to buggy client implementation of SubscribeAsync)
        //client.SubscribeAsync(o.Topic).Wait(cts.Token);
        //Task.Delay(10).Wait();

        // V2
        client.InternalClient.SubscribeAsync(o.Topic).Wait(cts.Token);
      }
      else {
        pendingSubscriptions.Add(o);
      }
    }

    public void Subscribe(Action<IMessage, CancellationToken> handler, CancellationToken? token = null) {
      var o = (SubscriptionOptions)configuration.DefaultSubscriptionOptions.Clone();      
      Subscribe(o);
    }

    public void Subscribe(string topic, Action<IMessage, CancellationToken> handler, CancellationToken? token = null) {
      var o = (SubscriptionOptions)configuration.DefaultSubscriptionOptions.Clone();
      o.Topic = topic;
      Subscribe(o);
    }

    public void Subscribe(SubscriptionOptions options, Action<IMessage, CancellationToken> handler, CancellationToken? token = null) {
      var o = options != null ? options : configuration.DefaultSubscriptionOptions;

      if (!actions.ContainsKey(o)) actions.Add(o, new List<ActionItem>());
      CancellationToken tok = token.HasValue ? token.Value : cts.Token;
      actions[o].Add(new ActionItem(handler, tok));

      Subscribe(o);
    }

    public void Subscribe<T>(Action<IMessage, CancellationToken> handler, CancellationToken? token = null) {
      var o = (SubscriptionOptions)configuration.DefaultSubscriptionOptions.Clone();

      Subscribe<T>(o, handler, token);
    }

    public void Subscribe<T>(string topic, Action<IMessage, CancellationToken> handler, CancellationToken? token = null) {
      var o = (SubscriptionOptions)configuration.DefaultSubscriptionOptions.Clone();
      o.Topic = topic;
      o.ContentType= typeof(T);
      o.ContentTypeFullName= o.ContentType.FullName;

      Subscribe<T>(o, handler, token);
    }

    public void Subscribe<T>(SubscriptionOptions options, Action<IMessage, CancellationToken> handler, CancellationToken? token = null) {
      var o = options != null ? options : configuration.DefaultSubscriptionOptions; // use new or default options as base
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

    public void Publish<T>(T payload) {
      var o = (PublicationOptions)configuration.DefaultPublicationOptions.Clone();      
      Publish(o, payload);
    }

    public void Publish<T>(string topic, T payload) {
      var o = (PublicationOptions)configuration.DefaultPublicationOptions.Clone();
      o.Topic = topic;
      Publish(o, payload);
    }

    public void Publish<T>(PublicationOptions options, T payload) {
      var task = PublishAsync(options, payload);
      task.Wait(cts.Token);
    }

    public async Task PublishAsync<T>(T payload) {
      var o = (PublicationOptions)configuration.DefaultPublicationOptions.Clone();      
      await PublishAsync(o, payload);
    }

    public async Task PublishAsync<T>(string topic, T payload) {
      var o = (PublicationOptions)configuration.DefaultPublicationOptions.Clone();
      o.Topic = topic;
      await PublishAsync(o, payload);
    }

    public async Task PublishAsync<T>(PublicationOptions options, T payload) {
      var o = options != null ? options : configuration.DefaultPublicationOptions;

      // setup message      
      var msg = new Message<T>(Configuration.Id, Configuration.Name, o.Topic, o.ResponseTopic, typeof(T).FullName, converter.Serialize<T>(payload), payload);
      //var msg = new Message<T>(Configuration.Id, Configuration.Name, o.Topic, o.ResponseTopic, typeof(T).FullName, null, payload);

      var appMessage = new MqttApplicationMessageBuilder()
        .WithTopic(msg.Topic)
        //.WithResponseTopic(msg.ResponseTopic)
        .WithPayload(converter.Serialize(msg))
        .WithQualityOfServiceLevel(GetQosLevel(o.QosLevel))
        .Build();

      var mappMessage = new ManagedMqttApplicationMessageBuilder()
        .WithApplicationMessage(appMessage)        
        .Build();

      await client.EnqueueAsync(mappMessage);
    }

    public T Request<T>() {
      var o = (RequestOptions)configuration.DefaultRequestOptions.Clone();

      return Request<T>(o);
    }

    public T Request<T>(string topic, string responseTopic, bool generateResponseTopicPostfix = true) {
      var o = (RequestOptions)configuration.DefaultRequestOptions.Clone();
      o.Topic = topic;
      o.ResponseTopic = responseTopic;
      o.GenerateResponseTopicPostfix = generateResponseTopicPostfix;

      return Request<T>(o);
    }

    public T Request<T>(RequestOptions options) {
      if (!IsConnected()) throw new Exception("MqttSocket: Socket must be connected before a blocking request can be made.");
            
      return RequestAsync<T>(options).Result;
    }

    public async Task<T> RequestAsync<T>() {
      return await RequestAsync<T, object>(null);
    }

    public async Task<T> RequestAsync<T>(string topic, string responseTopic, bool generateResponseTopicPostfix) {
      return await RequestAsync<T, object>(topic, responseTopic, generateResponseTopicPostfix, null);
    }

    public async Task<T> RequestAsync<T>(RequestOptions options) {         
      return await RequestAsync<T, object>(options, null);
    }

    public T1 Request<T1, T2>(T2 payload) {
      var o = (RequestOptions)configuration.DefaultRequestOptions.Clone();

      return Request<T1, T2>(o, payload);
    }

    public T1 Request<T1, T2>(string topic, string responseTopic, bool generateResponseTopicPostfix, T2 payload) {
      var o = (RequestOptions)configuration.DefaultRequestOptions.Clone();
      o.Topic = topic;
      o.ResponseTopic = responseTopic;
      o.GenerateResponseTopicPostfix = generateResponseTopicPostfix;

      return Request<T1, T2>(o, payload);
    }

    public T1 Request<T1, T2>(RequestOptions options, T2 payload) {
      if (!IsConnected()) throw new Exception("MqttSocket: Socket must be connected before a blocking request can be made.");

      return RequestAsync<T1, T2>(options, payload).Result;
    }

    public async Task<T1> RequestAsync<T1, T2>(T2 payload) {
      var o = (RequestOptions)configuration.DefaultRequestOptions.Clone();

      return await RequestAsync<T1, T2>(o, payload);
    }

    public async Task<T1> RequestAsync<T1, T2>(string topic, string responseTopic, bool generateResponseTopicPostfix, T2 payload) {
      var o = (RequestOptions)configuration.DefaultRequestOptions.Clone();
      o.Topic = topic;
      o.ResponseTopic = responseTopic;
      o.GenerateResponseTopicPostfix = generateResponseTopicPostfix;

      return await RequestAsync<T1, T2>(o, payload);
    }

    public async Task<T1> RequestAsync<T1, T2>(RequestOptions options, T2 payload) {
      // parse options
      var o = options != null ? (RequestOptions)options.Clone() : (RequestOptions)configuration.DefaultRequestOptions.Clone();
      var rt = o.GenerateResponseTopicPostfix
        ? string.Concat(o.ResponseTopic, "/", Misc.GenerateId(10))
        : o.ResponseTopic;      
      o.ResponseTopic = rt;
      o.ContentType = typeof(T1);
      o.ContentTypeFullName = o.ContentType.FullName;

      // configure promise
      var promise = new TaskCompletionSource<IMessage>();
      promises.Add(o, promise);
      Subscribe(o.GetResponseSubscriptionOptions());

      // build request message
      var appMessageBuilder = new MqttApplicationMessageBuilder()
        .WithTopic(o.Topic)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce);
        //.WithResponseTopic(o.ResponseTopic)
        //.WithUserProperty(Configuration.Name, Configuration.Name);

      // setup message
      string contentType = payload != null ? typeof(T2).FullName : "";
      //IMessage msg = new Message<T2>(Configuration.Id, Configuration.Name, o.Topic, o.ResponseTopic, contentType, payload);
      var msg = new Message<T2>(Configuration.Id, Configuration.Name, o.Topic, o.ResponseTopic, contentType, converter.Serialize<T2>(payload), payload);
      var serMsg = converter.Serialize(msg);

      appMessageBuilder = msg != null
        ? appMessageBuilder.WithPayload(serMsg)
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
        instance.Payload = msg.Payload != null ? msg.Payload.ToArray() : null;
        instance.ContentType = msg.ContentType;

        instance.Content = converter.Deserialize(msg.Payload, type);
        return instance;
      }
    }

    #endregion helper
  }
}
