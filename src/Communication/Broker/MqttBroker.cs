using Ai.Hgb.Dat.Configuration;
using MQTTnet;
using MQTTnet.Server;
using MQTTnet.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

// https://blog.behroozbc.ir/c-mqtt-broker-using-mqttnet-version-4
namespace Ai.Hgb.Dat.Communication {
  public sealed class MqttBroker : IBroker {
    public HostAddress Address {
      get { return address; }
      private set { if (!value.Equals(address)) address = value; }
    }

    public bool ConsoleLogging {
      get => consoleLogging;
      set => consoleLogging = value;
    }

    private HostAddress address;
    private MqttServer server;
    private bool consoleLogging;

    public MqttBroker(HostAddress address, bool consoleLogging = false) {
      this.address = address;
      this.consoleLogging = consoleLogging;
    }

    public void Dispose() {
      TearDown();
      server.Dispose();
      server = null;
      address = null;
    }

    public IBroker StartUp() {
      var t = StartUpAsync();
      t.Wait();

      return this;
    }


    private WebApplication webapp;
    private CancellationTokenSource webappCts;
    public void StartUpWebsocket() {
      webappCts = new CancellationTokenSource();
      var builder = WebApplication.CreateBuilder();
      builder.WebHost.UseKestrel(o =>
      {
        o.ListenAnyIP(1883, l => l.UseMqtt());
        o.ListenAnyIP(5000);
      });

      var optionsBuilder = new MqttServerOptionsBuilder()
        //.WithDefaultEndpoint()
        //.WithPersistentSessions() // enables QOS-Level 3                
        .WithDefaultEndpointPort(Address.Port);

      builder.Services.AddMqttServer(optionsBuilder => optionsBuilder.Build());
      builder.Services.AddMqttConnectionHandler();
      builder.Services.AddConnections();
      builder.Services.AddMqttWebSocketServerAdapter();

      webapp = builder.Build();
      server = webapp.Services.GetService<MqttServer>();
      server.InterceptingSubscriptionAsync += Server_InterceptingSubscriptionAsync;
      server.InterceptingPublishAsync += Server_InterceptingPublishAsync;
      server.ClientConnectedAsync += Server_ClientConnectedAsync;
      server.ClientDisconnectedAsync += Server_ClientDisconnectedAsync;
      server.StartedAsync += Server_StartedAsync;
      server.StoppedAsync += Server_StoppedAsync;

      webapp.UseRouting();
      //webapp.UseMqttEndpoint();
      webapp.UseEndpoints(endpoints =>
      {
        //endpoints.MapMqtt("");
        endpoints.MapMqtt("/mqtt");
        //endpoints.MapConnectionHandler<MqttConnectionHandler>(
        //  "/mqtt",
        //  httpConnectionDispatcherOptions => httpConnectionDispatcherOptions.WebSockets.SubProtocolSelector = protocolList => protocolList.FirstOrDefault() ?? string.Empty);

      });

      //webapp.UseMqttServer(s => { });

      webapp.RunAsync(webappCts.Token);
    }

    public Task StartUpAsync() {

      StartUpWebsocket();
      return Task.CompletedTask;

      //var optionsBuilder = new MqttServerOptionsBuilder()
      //  .WithDefaultEndpoint()
      //  .WithPersistentSessions() // enables QOS-Level 3                
      //  .WithDefaultEndpointPort(Address.Port);


      //server = new MqttFactory().CreateMqttServer(optionsBuilder.Build());                  
      //server.InterceptingSubscriptionAsync += Server_InterceptingSubscriptionAsync;
      //server.InterceptingPublishAsync += Server_InterceptingPublishAsync;
      //server.ClientConnectedAsync += Server_ClientConnectedAsync;
      //server.ClientDisconnectedAsync += Server_ClientDisconnectedAsync;
      //server.StartedAsync += Server_StartedAsync;
      //server.StoppedAsync += Server_StoppedAsync;

      //return server.StartAsync();
    }


    public void TearDown() {
      var t = TearDownAsync();

      if (consoleLogging) Console.WriteLine("Shutdown");
      webappCts.Cancel();
      webapp.WaitForShutdown();

      t.Wait();
    }

    public Task TearDownAsync() {
      return server.StopAsync();
    }

    private Task Server_StartedAsync(EventArgs arg) {
      if (consoleLogging) Console.WriteLine($"MqttBroker: Broker started.");
      return Task.CompletedTask;
    }

    private Task Server_StoppedAsync(EventArgs arg) {
      if (consoleLogging) Console.WriteLine($"MqttBroker: Broker stopped.");
      return Task.CompletedTask;
    }

    private Task Server_ClientConnectedAsync(ClientConnectedEventArgs arg) {
      if (consoleLogging) Console.WriteLine($"MqttBroker: Client {arg.ClientId} connected.");
      return Task.CompletedTask;
    }

    private Task Server_ClientDisconnectedAsync(ClientDisconnectedEventArgs arg) {
      if (consoleLogging) Console.WriteLine($"MqttBroker: Client {arg.ClientId} disconnected.");
      return Task.CompletedTask;
    }

    private Task Server_InterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs arg) {
      if (consoleLogging) Console.WriteLine($"MqttBroker: Client {arg.ClientId} subscribed to topic {arg.TopicFilter.Topic}.");
      return Task.CompletedTask;
    }

    private Task Server_InterceptingPublishAsync(InterceptingPublishEventArgs arg) {
      if (consoleLogging) Console.WriteLine($"MqttBroker: Client {arg.ClientId} sends message to topic {arg.ApplicationMessage.Topic}.");
      return Task.CompletedTask;
    }
  }
}
