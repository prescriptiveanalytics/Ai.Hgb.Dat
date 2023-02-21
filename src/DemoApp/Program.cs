using DCT.Communication;
using System.Diagnostics;

namespace DCT.DemoApp {

  public class Program {

    static HostAddress address;
    static IPayloadConverter converter;

    static void Main(string[] args) {
      var cts = new CancellationTokenSource();
      var sw = new Stopwatch();
      sw.Start();

      address = new HostAddress("127.0.0.1", 1883);
      converter = new JsonPayloadConverter();

      IBroker broker = new MqttBroker(address);
      broker.StartUp();      

      string pubsubTopic = "demoapp/docs";
      string respTopic = "demoapp/responses";
      string reqTopic = "demoapp/specialdoc";
      var pubOptions = new PublicationOptions(pubsubTopic, respTopic, QualityOfServiceLevel.ExactlyOnce);
      var subOptions = new SubscriptionOptions(pubsubTopic, QualityOfServiceLevel.ExactlyOnce);
      var reqOptions = new RequestOptions(reqTopic, respTopic, true);

      ISocket producerOne = new MqttSocket(address, converter, subOptions, pubOptions, reqOptions);
      ISocket producerTwo = new MqttSocket(address, converter, subOptions, pubOptions, reqOptions);
      ISocket consumerOne = new MqttSocket(address, converter, subOptions, pubOptions, reqOptions);
      ISocket consumerTwo = new MqttSocket(address, converter, subOptions, pubOptions, reqOptions);

      consumerOne.Subscribe(ProcessDocument, cts.Token); // v1
      //consumerOne.Subscribe<Document>(ProcessDocument, cts.Token); // v2


      producerOne.Connect();
      producerTwo.Connect();
      consumerOne.Connect();
      consumerTwo.Connect();

      // do work
      ProduceDocuments(producerOne);


      Console.WriteLine("Waiting for completion...");
      Thread.Sleep(10000);
      // tear down
      producerOne.Disconnect();
      producerTwo.Disconnect();
      consumerOne.Disconnect();
      consumerTwo.Disconnect();
      broker.TearDown();


      sw.Stop();
      Console.WriteLine($"\n\nTime elapsed: {sw.Elapsed.TotalMilliseconds / 1000.0:f4} seconds");
      Console.WriteLine();
    }

    public static void ProduceDocuments(ISocket socket) {
      var rnd = new Random();
      var t = Task.Factory.StartNew(() =>
      {
        for (int i = 0; i < 10; i++) {
          var doc = new Document(i+1, "one", "lorem ipsum dolor");
          Task.Delay(500 + rnd.Next(1000)).Wait();
          Console.WriteLine($"Produced doc: {doc}");
          socket.Publish(doc);
        }
      });

      t.Wait();
    }


    public static void ProcessDocument(IMessage docMsg, CancellationToken token) {
      Document doc = null;
      if (docMsg.Content != null && docMsg.Content is Document) doc = (Document)docMsg.Content;
      else doc = converter.Deserialize<Document>(docMsg.Payload);

      var rnd = new Random();
      Thread.Sleep(500 + rnd.Next(1000));      
      if (token.IsCancellationRequested) {
        Console.WriteLine($"Cancelled processing document {doc.Id}.");
        return;
      }              
      Console.WriteLine($"Processed doc: {doc.ToString()}");
    }
  }

  public class Document {

    public int Id { get; set; }
    public string Author { get; set; }
    public string Text { get; set; }

    public Document(int id, string author, string text) {
      Id = id;
      Author = author;
      Text = text;
    }

    public override string ToString() {
      return $"{Id} / {Author}: {Text}";
    }
  }
}