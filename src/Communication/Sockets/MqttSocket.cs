﻿using DCT.Utils;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// TODO: Default sending QOS?
// TODO: blocking action flag
// TODO: sub/pending handling
// TODO: implement all open stubs

namespace DCT.Communication {
  public class MqttSocket : ISocket {
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

    public event EventHandler<EventArgs<Message>> MessageReceived;

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

    private Dictionary<string, List<ActionItem>> actions;
    private Dictionary<string, List<TaskCompletionSource<Message>>> promises;

    public MqttSocket(HostAddress address, IPayloadConverter converter) {
      this.address = address;
      this.converter = converter;

      cts = new CancellationTokenSource();
      client = new MqttFactory().CreateManagedMqttClient();

      subscriptions = new HashSet<SubscriptionOptions>();
      pendingSubscriptions = new HashSet<SubscriptionOptions>();
      actions = new Dictionary<string, List<ActionItem>>();
      promises = new Dictionary<string, List<TaskCompletionSource<Message>>>();
      blockingActionExecution = false;

      client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
    }

    private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg) {
      // parse received message
      var msg = new Message(arg.ClientId, 
        arg.ApplicationMessage.ContentType, 
        arg.ApplicationMessage.Payload, 
        arg.ApplicationMessage.Topic, 
        arg.ApplicationMessage.ResponseTopic);

      // collect actions and promises to be executed
      var actionList = new List<ActionItem>();
      var promiseList = new List<TaskCompletionSource<Message>>();


      lock(actions) {
        foreach(var item in actions) {
          if(Misc.CompareTopics(item.Key, msg.Topic)) {
            actionList.AddRange(item.Value);
          }
        }
      }

      lock(promises) {
        foreach(var item in promises) {
          if(Misc.CompareTopics(item.Key, msg.Topic)) {
            promiseList.AddRange(item.Value);
          }
        }
      }

      // execute collected actions and promises
      Task t;
      if(blockingActionExecution) {
        // v1: async (intended socket behavior)
        t = Task.Factory.StartNew(() =>
        {
          foreach (var item in actionList) {
            if (!cts.IsCancellationRequested) {
              item.Action(msg, item.Token);
            }
          }
          foreach (var item in promiseList) {
            if (!cts.IsCancellationRequested) {
              item.TrySetResult(msg);
            }
          }

        }, cts.Token);
      } else {
        // v2: blocking (threadsafe behavior regarding processing order)
        foreach (var item in actionList) {
          if (!cts.IsCancellationRequested) {
            item.Action(msg, item.Token);
          }
        }
        foreach (var item in promiseList) {
          if (!cts.IsCancellationRequested) {
            item.TrySetResult(msg);
          }
        }
        t = Task.CompletedTask;
      }

      return t;
    }

    public bool Connect() {
      if (IsConnected()) return true;

      var options = new MqttClientOptionsBuilder()
        .WithTcpServer(address.Server, address.Port);
      var mgOptions = new ManagedMqttClientOptionsBuilder()
        .WithClientOptions(options.Build())
        .Build();

      client.StartAsync(mgOptions).Wait(cts.Token);     

      if(defaultSubscriptionOptions != null && defaultSubscriptionOptions.Topic != null)
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
      if(IsConnected()) {
        subscriptions.Add(options);
        client.SubscribeAsync(options.Topic, GetQosLevel(options.QosLevel)).Wait(cts.Token);
      } else {
        pendingSubscriptions.Add(options);
      }
    }

    public void Subscribe(Action<Message, CancellationToken> handler, CancellationToken? token = null, SubscriptionOptions options = null) {
      if (options != null) Subscribe(options);
      else options = DefaultSubscriptionOptions;

      if (!actions.ContainsKey(options.Topic)) actions.Add(options.Topic, new List<ActionItem>());
      CancellationToken tok = token.HasValue ? token.Value : cts.Token;
      actions[options.Topic].Add(new ActionItem(handler, tok));
    }

    public void Unsubscribe(string topic = null) {
      if(topic != null) {
        client.UnsubscribeAsync(topic).Wait(cts.Token);
        subscriptions.RemoveWhere(s => s.Topic == topic);
      } else {
        client.UnsubscribeAsync(subscriptions.Select(x => x.Topic).ToList()).Wait(cts.Token);
        subscriptions.Clear();
      }
    }

    public void Publish<T>(T message, PublicationOptions options = null) {
      var task = PublishAsync(message, options);
      task.Wait(cts.Token);
    }

    public async Task PublishAsync<T>(T message, PublicationOptions options = null) {
      var o = options != null ? options : DefaultPublicationOptions;

      var appMessage = new MqttApplicationMessageBuilder()
        .WithTopic(o.Topic)
        .WithResponseTopic(o.ResponseTopic)
        .WithPayload(converter.Serialize(message))
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

    public async Task<T1> RequestAsync<T1, T2>(T2 message, RequestOptions options = null) {
      // parse options
      var o = options != null ? options : DefaultRequestOptions;
      var rt = o.GenerateResponseTopicPostfix 
        ? string.Concat(o.ResponseTopic, "/", Misc.GenerateId(10))
        : o.ResponseTopic;
      o.ResponseTopic= rt;

      // configure promise
      var promise = new TaskCompletionSource<Message>();
      if (!promises.ContainsKey(o.ResponseTopic)) promises.Add(o.ResponseTopic, new List<TaskCompletionSource<Message>>());
      promises[o.ResponseTopic].Add(promise);
      Subscribe(o.GetSubscriptionOptions());

      // build message
      var appMessageBuilder = new MqttApplicationMessageBuilder()
        .WithTopic(o.Topic)
        .WithResponseTopic(o.ResponseTopic)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce);

      appMessageBuilder = message != null
        ? appMessageBuilder.WithPayload(converter.Serialize(message))
        : appMessageBuilder;

      var mappMessage = new ManagedMqttApplicationMessageBuilder()
        .WithApplicationMessage(appMessageBuilder.Build())
        .Build();

      // send message
      await client.EnqueueAsync(mappMessage);

      // await response
      var response = await promise.Task;

      // deregister promise handling
      Unsubscribe(o.ResponseTopic);      
      promises.Remove(o.ResponseTopic);

      // deserialize and return response
      return converter.Deserialize<T1>(response.Payload);
    }

    #region helper
    private MqttQualityOfServiceLevel GetQosLevel(QualityOfServiceLevel qosl) {
      if (qosl == QualityOfServiceLevel.AtMostOnce) return MqttQualityOfServiceLevel.AtMostOnce;
      else if (qosl == QualityOfServiceLevel.AtLeastOnce) return MqttQualityOfServiceLevel.AtLeastOnce;
      else return MqttQualityOfServiceLevel.ExactlyOnce;
    }
    #endregion helper
  }
}
