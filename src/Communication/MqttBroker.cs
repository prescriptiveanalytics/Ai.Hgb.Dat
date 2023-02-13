using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        .WithDefaultEndpointPort(Address.Port);


      server = new MqttFactory().CreateMqttServer(optionsBuilder.Build());      
      return server.StartAsync();

    }

    public void TearDown() {
      throw new NotImplementedException();
    }

    public Task TearDownAsync() {
      throw new NotImplementedException();
    }
  }
}
