using MQTTnet;
using MQTTnet.Server;

// https://blog.behroozbc.ir/c-mqtt-broker-using-mqttnet-version-4
namespace DCT.Communication {
  public sealed class MqttBroker : IBroker {
    public HostAddress Address {
      get { return address; }
      private set { if (!value.Equals(address)) address = value; }
    }

    private HostAddress address;
    private MqttServer server;

    public MqttBroker(HostAddress address) {
      this.address = address;
    }

    public void StartUp() {
      var t = StartUpAsync();
      t.Wait();
    }

    public Task StartUpAsync() {

      var optionsBuilder = new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithPersistentSessions() // enables QOS-Level 3        
        .WithDefaultEndpointPort(Address.Port);
      
      server = new MqttFactory().CreateMqttServer(optionsBuilder.Build());      
      
      server.InterceptingSubscriptionAsync += Server_InterceptingSubscriptionAsync;
      server.InterceptingPublishAsync += Server_InterceptingPublishAsync;
      server.ClientConnectedAsync += Server_ClientConnectedAsync;
      server.ClientDisconnectedAsync += Server_ClientDisconnectedAsync;
      server.StartedAsync += Server_StartedAsync;
      server.StoppedAsync += Server_StoppedAsync;
      
      return server.StartAsync();
    }


    public void TearDown() {
      var t = TearDownAsync();
      t.Wait();
    }

    public Task TearDownAsync() {
      return server.StopAsync();
    }

    private Task Server_StartedAsync(EventArgs arg) {
      Console.WriteLine($"MqttBroker: Broker started.");
      return Task.CompletedTask;
    }

    private Task Server_StoppedAsync(EventArgs arg) {
      Console.WriteLine($"MqttBroker: Broker stopped.");
      return Task.CompletedTask;
    }

    private Task Server_ClientConnectedAsync(ClientConnectedEventArgs arg) {
      Console.WriteLine($"MqttBroker: Client {arg.ClientId} connected.");
      return Task.CompletedTask;
    }

    private Task Server_ClientDisconnectedAsync(ClientDisconnectedEventArgs arg) {
      Console.WriteLine($"MqttBroker: Client {arg.ClientId} disconnected.");
      return Task.CompletedTask;
    }

    private Task Server_InterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs arg) {      
      Console.WriteLine($"MqttBroker: Client {arg.ClientId} subscribed to topic {arg.TopicFilter.Topic}.");      
      return Task.CompletedTask;
    }

    private Task Server_InterceptingPublishAsync(InterceptingPublishEventArgs arg) {      
      Console.WriteLine($"MqttBroker: Client {arg.ClientId} sends message to topic {arg.ApplicationMessage.Topic}.");
      return Task.CompletedTask;
    }
  }
}
