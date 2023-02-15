using DCT.Communication;
using System.Diagnostics;

namespace DCT.DemoApp {

  public class Program {

    static HostAddress address;    

    static void Main(string[] args) {
      var sw = new Stopwatch();
      sw.Start();

      address = new HostAddress("127.0.0.1", 1883);
      IPayloadConverter converter = new JsonPayloadConverter();

      IBroker broker = new MqttBroker(address);
      ISocket producer = new MqttSocket(address, converter);
      ISocket consumerOne = new MqttSocket(address, converter);
      ISocket consumerTwo = new MqttSocket(address, converter);

      //broker.StartUp();
      //producer.Connect();
      //consumerOne.Connect();
      //consumerTwo.Connect();



      sw.Stop();
      Console.WriteLine($"\n\nTime elapsed: {sw.Elapsed.TotalMilliseconds / 1000.0:f4} seconds");
      Console.WriteLine();
    }

  }
}