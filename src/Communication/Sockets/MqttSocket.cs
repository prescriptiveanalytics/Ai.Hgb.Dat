using DCT.Utils;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.Communication {
  public class MqttSocket : ISocket {
    public HostAddress Address {
      get { return address; }
    }

    public IEnumerable<string> Subscriptions {
      get { return subscriptions; }
    }

    public string BaseTopic {
      get { return baseTopic; } 
      set { baseTopic = value; }
    }

    public QualityOfServiceLevel QOSLevel { 
      get { return qosLevel; }
      set { qosLevel = value; }
    }

    public IPayloadConverter Converter { 
      get { return converter; }
      set { converter = value; } 
    }    

    public event EventHandler<EventArgs<Message>> MessageReceived;

    private HostAddress address;
    private HashSet<string> subscriptions;
    private string baseTopic;
    private QualityOfServiceLevel qosLevel;
    private IPayloadConverter converter;

    private IManagedMqttClient client;
    private CancellationTokenSource cts;

    private Dictionary<string, List<ActionItem>> actions;
    private Dictionary<string, List<TaskCompletionSource<Message>>> promises;

    public MqttSocket(HostAddress address, IPayloadConverter converter) {
      this.address = address;
      this.converter = converter;

      cts = new CancellationTokenSource();
      client = new MqttFactory().CreateManagedMqttClient();

      subscriptions = new HashSet<string>();
      actions = new Dictionary<string, List<ActionItem>>();
      promises = new Dictionary<string, List<TaskCompletionSource<Message>>>();

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
      var t = Task.Factory.StartNew(() => {
        foreach(var item in actionList) {
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


      return t;
    }

    public bool Connect() {
      var options = new MqttClientOptionsBuilder()
        .WithTcpServer(address.Server, address.Port);
      var mgOptions = new ManagedMqttClientOptionsBuilder()
        .WithClientOptions(options.Build())
        .Build();

      client.StartAsync(mgOptions).Wait();

      return client.IsConnected;
    }

    public bool Disconnect() {
      cts.Cancel();
      client.StopAsync().Wait();
      client.Dispose();

      return client.IsConnected;
    }

    public void Abort() {
      cts.Cancel();
      client.Dispose();
      client = null;
    }

    public bool IsConnected() {
      return client.IsConnected;
    }

    public void Publish<T>(T message, string? topic = null) {
      throw new NotImplementedException();
    }

    public Task PublishAsync<T>(T message, string? topic = null) {
      throw new NotImplementedException();
    }

    public T Request<T>(string? topic = null) {
      throw new NotImplementedException();
    }

    public T1 Request<T1, T2>(T2 message, string? topic = null) {
      throw new NotImplementedException();
    }

    public Task<T> RequestAsync<T>(string? topic = null) {
      throw new NotImplementedException();
    }

    public Task<T1> RequestAsync<T1, T2>(T2 message, string? topic = null) {
      throw new NotImplementedException();
    }

    public void Subscribe(string topic) {
      throw new NotImplementedException();
    }

    public void Subscribe(Action<Message, CancellationToken> handler, CancellationToken? token = null, string? topic = null) {
      
      lock(actions) {
        // TODO
      }


    }

    public void Unsubscribe(string? topic = null) {
      throw new NotImplementedException();
    }
  }
}
