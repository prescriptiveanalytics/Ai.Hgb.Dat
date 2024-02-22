using Ai.Hgb.Dat.Configuration;
using MQTTnet;
using MQTTnet.Server;
using MQTTnet.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Configuration;

// https://blog.behroozbc.ir/c-mqtt-broker-using-mqttnet-version-4
namespace Ai.Hgb.Dat.Communication {
  public sealed class MqttBroker : IBroker {
    public HostAddress Address {
      get { return address; }
      set { address = value; }
    }
    public int WebsocketPort { 
      get { return websocketPort; }
      set { websocketPort = value; }
    }

    public string WebsocketPattern { 
      get { return websocketPattern; }
      set { websocketPattern = value; }
    }

    public bool MqttEnabled {
      get { return mqttEnabled; }
      set { mqttEnabled = value; }
    }

    public bool WebsocketEnabled {
      get { return websocketEnabled; }
      set { websocketEnabled = value; }
    }

    public Microsoft.Extensions.Configuration.IConfiguration LogConfig { get; set; }

    private HostAddress address;
    private int websocketPort;
    private string websocketPattern;
    private bool mqttEnabled, websocketEnabled;

    private MqttServer server;
    private WebApplication webapp;
    private CancellationTokenSource webappCts;


    public MqttBroker(HostAddress address, bool mqttEnabled = true, bool websocketEnabled = true, int websocketPort = 5000, string websocketPattern = "mqtt") {
      this.address = address;
      this.mqttEnabled = mqttEnabled;
      this.websocketEnabled = websocketEnabled;
      this.websocketPort = websocketPort;
      this.websocketPattern = websocketPattern;
    }

    public void Dispose() {
      TearDown();
      server.Dispose();
      server = null;
      address = null;
    }

    public IBroker StartUp() {
      var t = StartUpAsync();
      return this;
    }

    public Task StartUpAsync() {
      return StartUpServerAsync(mqttEnabled, websocketEnabled);
    }

    public void TearDown() {
      var t = TearDownAsync();

      //if (consoleLogging) Console.WriteLine("Shutdown");
      webappCts.Cancel();
      webapp.WaitForShutdown();

      t.Wait();
    }

    public Task TearDownAsync() {
      return server.StopAsync();
    }

    private Task StartUpMqttAsync() {
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

    private Task StartUpServerAsync(bool mqttEnabled = true, bool websocketEnabled = true) {
      webappCts = new CancellationTokenSource();
      var builder = WebApplication.CreateBuilder();
      builder.WebHost.UseKestrel(o =>
      {
        o.ListenAnyIP(address.Port, l => l.UseMqtt());
        if(websocketEnabled) o.ListenAnyIP(websocketPort);
      });

      // setup logger
      if(LogConfig != null) {
        Log.Logger = new LoggerConfiguration()
          .ReadFrom.Configuration(LogConfig).CreateLogger();        
      } else {        
        Log.Logger = new LoggerConfiguration()
          .WriteTo.Console().CreateLogger();
      }
      builder.Host.UseSerilog();

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
        if (websocketEnabled) endpoints.MapMqtt($"/{websocketPattern}");
        //endpoints.MapConnectionHandler<MqttConnectionHandler>(
        //  "/mqtt",
        //  httpConnectionDispatcherOptions => httpConnectionDispatcherOptions.WebSockets.SubProtocolSelector = protocolList => protocolList.FirstOrDefault() ?? string.Empty);

      });

      //webapp.UseMqttServer(s => { });

      return webapp.RunAsync(webappCts.Token);
    }


    #region event monitoring/interception

    private Task Server_StartedAsync(EventArgs arg) {
      Log.Information($"Broker started.");
      return Task.CompletedTask;
    }

    private Task Server_StoppedAsync(EventArgs arg) {
      Log.Information($"Broker stopped.");
      return Task.CompletedTask;
    }

    private Task Server_ClientConnectedAsync(ClientConnectedEventArgs arg) {
      Log.Information($"Client {arg.ClientId} connected.");
      return Task.CompletedTask;
    }

    private Task Server_ClientDisconnectedAsync(ClientDisconnectedEventArgs arg) {
      Log.Information($"Client {arg.ClientId} disconnected.");
      return Task.CompletedTask;
    }

    private Task Server_InterceptingSubscriptionAsync(InterceptingSubscriptionEventArgs arg) {
      Log.Information($"Client {arg.ClientId} subscribed to topic {arg.TopicFilter.Topic}.");
      return Task.CompletedTask;
    }

    private Task Server_InterceptingPublishAsync(InterceptingPublishEventArgs arg) {
      Log.Information($"Client {arg.ClientId} sends message to topic {arg.ApplicationMessage.Topic}.");
      return Task.CompletedTask;
    }

    #endregion event monitoring/interception
  }
}
