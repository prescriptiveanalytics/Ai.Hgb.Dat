using DAT.Communication;
using DAT.Configuration;
using System.Diagnostics;
using System.Net;

namespace DAT.DemoApp {

  public class Program {

    static IPayloadConverter converter;    
    static CountdownEvent ce;

    static void Main(string[] args) {
      var sw = new Stopwatch();
      sw.Start();

      //RunDemo_Mqtt_DocProducerConsumer();
      //RunDemo_Mqtt_DocRequestResponse();
      //RunDemo_ApacheKafka_ProducerConsumer();
      //RunDemo_ReadConfigurations();
      //RunDemo_ConfigurationMonitorBasedProducerConsumer();
      RunDemo_DisposableBrokerSocket();

      sw.Stop();
      Console.WriteLine($"\n\nTime elapsed: {sw.Elapsed.TotalMilliseconds / 1000.0:f4} seconds");
      Console.WriteLine();
    }

    public static void RunDemo_ConfigurationMonitorBasedProducerConsumer() {
      var cts = new CancellationTokenSource();
      int jobsPerProducer = 10;
      ce = new CountdownEvent(jobsPerProducer);

      var pMonitor = new DAT.Configuration.Monitor<SocketConfiguration>();
      var cMonitor = new DAT.Configuration.Monitor<SocketConfiguration>();
      pMonitor.Initialize(@"..\..\..\Configurations\ProducerConfig.yml");
      cMonitor.Initialize(@"..\..\..\Configurations\ConsumerConfig.yml");

      pMonitor.Start();
      cMonitor.Start();

      
   

      // setup
      ISocket producerOne = new MqttSocket(pMonitor.Configuration);
      ISocket consumerOne = new MqttSocket(cMonitor.Configuration);

      // startuprf
      IBroker broker = new MqttBroker(pMonitor.Configuration.Broker);
      broker.StartUp();
      producerOne.Connect();
      consumerOne.Connect();

      // main work
      consumerOne.Subscribe<Document>(ProcessDocument, cts.Token);
      Task.Factory.StartNew(() => ProduceDocuments(producerOne, jobsPerProducer));

      // wait for completion
      Console.WriteLine("Waiting for completion...");
      ce.Wait();


      // tear down
      producerOne.Disconnect();
      consumerOne.Disconnect();
      broker.TearDown();
      pMonitor.Stop();
      cMonitor.Stop();
    }

    public static void RunDemo_ConfigurationBasedProducerConsumer() {            
      var cts = new CancellationTokenSource();
      int jobsPerProducer = 10;
      ce = new CountdownEvent(jobsPerProducer);      


      // setup
      SocketConfiguration pConfig = Parser.Parse<SocketConfiguration>(@"..\..\..\Configurations\ProducerConfig.yml");
      SocketConfiguration cConfig = Parser.Parse<SocketConfiguration>(@"..\..\..\Configurations\ConsumerConfig.yml");
      ISocket producerOne = new MqttSocket(pConfig);
      ISocket consumerOne = new MqttSocket(cConfig);

      // startup
      IBroker broker = new MqttBroker(pConfig.Broker);
      broker.StartUp();
      producerOne.Connect();
      consumerOne.Connect();

      // main work
      consumerOne.Subscribe<Document>(ProcessDocument, cts.Token);
      Task.Factory.StartNew(() => ProduceDocuments(producerOne, jobsPerProducer));

      // wait for completion
      Console.WriteLine("Waiting for completion...");
      ce.Wait();      


      // tear down
      producerOne.Disconnect();      
      consumerOne.Disconnect();      
      broker.TearDown();
    }

    public static void RunDemo_DisposableBrokerSocket() {
      var cts = new CancellationTokenSource();
      int jobsPerProducer = 10;
      SocketConfiguration pConfig = Parser.Parse<SocketConfiguration>(@"..\..\..\Configurations\ProducerConfig.yml");

      using(var broker = new MqttBroker(pConfig.Broker).StartUp()) {
        using (var producer = new MqttSocket(pConfig).Connect()) 
          ProduceDocuments(producer, jobsPerProducer);
        
      }
    }

    public static void RunDemo_ReadConfigurations() {
      IConfiguration config = Parser.Parse(@"..\..\..\Configurations\SocketConfig.yml");
      SocketConfiguration sConfig = Parser.Parse<SocketConfiguration>(@"..\..\..\Configurations\SocketConfig.yml");
    }

    public static void RunDemo_ApacheKafka_ProducerConsumer() {
      var cts = new CancellationTokenSource();
      int jobsPerProducer = 10;
      ce = new CountdownEvent(jobsPerProducer);
      
      HostAddress address = new HostAddress("localhost", 9092);
      converter = new JsonPayloadConverter();

      string pubsubTopic = "docs";
      string respTopic = "responses";
      string reqTopic = "specialdoc";
      //string pubsubTopic = "demoapp/docs";
      //string respTopic = "demoapp/responses";
      //string reqTopic = "demoapp/specialdoc";
      var pubOptions = new PublicationOptions(pubsubTopic, respTopic, QualityOfServiceLevel.ExactlyOnce);
      var subOptions = new SubscriptionOptions(pubsubTopic, QualityOfServiceLevel.ExactlyOnce);
      var reqOptions = new RequestOptions(reqTopic, respTopic, true);


      ISocket producerOne, consumerOne;
      producerOne = new ApachekafkaSocket("p1", "producerOne", address, converter, null, pubOptions, reqOptions);
      consumerOne = new ApachekafkaSocket("c1", "consumerOne", address, converter, subOptions, pubOptions, reqOptions);

      // necessary since confluent kafka's group coordination is weird
      Thread.Sleep(10000);

      // do work
      consumerOne.Subscribe(ProcessDocument, cts.Token);
      var t = Task.Factory.StartNew(() => ProduceDocuments(producerOne, jobsPerProducer));
      t.Wait();
      //ce.Wait();
      Thread.Sleep(2000);

      producerOne.Disconnect();
      consumerOne.Disconnect();
    }

    public static void RunDemo_Mqtt_DocRequestResponse() {
      var cts = new CancellationTokenSource();

      int requestsPerClient = 10;
      ce = new CountdownEvent(requestsPerClient * 2); // two clients

      HostAddress address = new HostAddress("127.0.0.1", 1883);
      converter = new JsonPayloadConverter();

      IBroker broker = new MqttBroker(address);
      broker.StartUp();

      string pubsubTopic = "demoapp/docs";
      string respTopic = "demoapp/responses";
      string reqTopic = "demoapp/docs";
      var pubOptions = new PublicationOptions(pubsubTopic, respTopic, QualityOfServiceLevel.ExactlyOnce);
      var subOptions = new SubscriptionOptions(pubsubTopic, QualityOfServiceLevel.ExactlyOnce);
      var reqOptions = new RequestOptions(reqTopic, respTopic, true);

      ISocket server = new MqttSocket("s1", "server", address, converter, null, pubOptions, reqOptions);
      ISocket clientOne, clientTwo;
      clientOne = new MqttSocket("c1", "clientOne", address, converter, null, pubOptions, reqOptions);
      clientTwo = new MqttSocket("c2", "clientTwo", address, converter, null, pubOptions, reqOptions);     

      server.Connect();
      clientOne.Connect();
      clientTwo.Connect();

      //Thread.Sleep(1000);

      // do work
      var serverTask = Task.Factory.StartNew(() => ServeDocuments(server, cts.Token), cts.Token);
      var clientTaskOne = Task.Factory.StartNew(() => RequestDocuments(clientOne, cts.Token, requestsPerClient), cts.Token);
      var clientTaskTwo = Task.Factory.StartNew(() => RequestDocuments(clientTwo, cts.Token, requestsPerClient), cts.Token);


      Console.WriteLine("Waiting for completion...");
      Task.WaitAll(new Task[] { clientTaskOne, clientTaskTwo });      


      // tear down
      server.Disconnect();
      clientOne.Disconnect();
      clientTwo.Disconnect();
      broker.TearDown();
    }

    public static void RunDemo_Mqtt_DocProducerConsumer() {
      var cts = new CancellationTokenSource();

      int jobsPerProducer = 10;
      ce = new CountdownEvent(40);

      HostAddress address = new HostAddress("127.0.0.1", 1883);
      converter = new JsonPayloadConverter();

      IBroker broker = new MqttBroker(address);
      broker.StartUp();

      string pubsubTopic = "demoapp/docs";
      string respTopic = "demoapp/responses";
      string reqTopic = "demoapp/specialdoc";
      var pubOptions = new PublicationOptions(pubsubTopic, respTopic, QualityOfServiceLevel.ExactlyOnce);
      var subOptions = new SubscriptionOptions(pubsubTopic, QualityOfServiceLevel.ExactlyOnce);
      var reqOptions = new RequestOptions(reqTopic, respTopic, true);

      ISocket producerOne, producerTwo, consumerOne, consumerTwo;
      producerOne = new MqttSocket("p1", "producerOne", address, converter, subOptions, pubOptions, reqOptions);
      producerTwo = new MqttSocket("p2", "producerTwo", address, converter, subOptions, pubOptions, reqOptions);
      consumerOne = new MqttSocket("c1", "consumerOne", address, converter, subOptions, pubOptions, reqOptions);
      consumerTwo = new MqttSocket("c2", "consumerTwo", address, converter, subOptions, pubOptions, reqOptions);

      producerOne.Connect();
      producerTwo.Connect();
      consumerOne.Connect();
      consumerTwo.Connect();

      consumerOne.Subscribe(ProcessDocument, cts.Token); // v1
      consumerTwo.Subscribe(ProcessDocument, cts.Token); // v1
      //consumerOne.Subscribe<Document>(ProcessDocument, cts.Token); // v2

      // do work
      Task.Factory.StartNew(() => ProduceDocuments(producerOne, jobsPerProducer));
      Task.Factory.StartNew(() => ProduceDocuments(producerTwo, jobsPerProducer));

      // wait for completion
      Console.WriteLine("Waiting for completion...");
      ce.Wait();
      

      // tear down
      producerOne.Disconnect();
      producerTwo.Disconnect();
      consumerOne.Disconnect();
      consumerTwo.Disconnect();
      broker.TearDown();
    }

    private static void RequestDocuments(ISocket socket, CancellationToken token, int jobCount) {
      
      for(int i = 0; i < jobCount; i++) {
        var doc = socket.Request<Document>();
        Console.WriteLine($"Client {socket.Configuration.Name} rocessing doc: {doc}");        
      }
    }

    private static void ServeDocuments(ISocket socket, CancellationToken token) {
      var rnd = new Random();
      var o = socket.Configuration.DefaultRequestOptions;
      var count = 0;
      
      socket.Subscribe((IMessage docReq, CancellationToken token) => {
        count = Interlocked.Increment(ref count);
        var doc = new Document(socket.Configuration.Id + "-" + count, "server", "lorem ipsum dolor");
        Task.Delay(500 + rnd.Next(1000)).Wait();
        Console.WriteLine($"Produced doc: {doc}");
        var pOpt = new PublicationOptions(docReq.ResponseTopic, "", QualityOfServiceLevel.ExactlyOnce);
        socket.Publish(doc, pOpt);
      }, token, o.GetRequestSubscriptionOptions());
    }

    private static void ProduceDocuments(ISocket socket, int jobCount) {
      var rnd = new Random();
      var t = Task.Factory.StartNew(() =>
      {
        for (int i = 0; i < jobCount; i++) {
          var doc = new Document(socket.Configuration.Id + "-" + (i+1), socket.Configuration.Name, "lorem ipsum dolor");
          Task.Delay(500 + rnd.Next(1000)).Wait();
          Console.WriteLine($"Produced doc: {doc}");
          socket.Publish(doc);
        }
      });

      t.Wait();
    }

    private static void ProcessDocument(IMessage docMsg, CancellationToken token) {
      Document doc = null;
      if (docMsg.Content != null && docMsg.Content is Document) doc = (Document)docMsg.Content;
      else doc = converter.Deserialize<Document>(docMsg.Payload);

      var rnd = new Random();
      Thread.Sleep(100 + rnd.Next(500));      
      if (token.IsCancellationRequested) {
        Console.WriteLine($"Cancelled processing document {doc.Id}.");
        return;
      }              
      
      lock (ce) {
        ce.Signal();
        Console.WriteLine($"Processed doc: {doc.ToString()}");
      }
    }
  }

  public class Document {

    public string Id { get; set; }
    public string Author { get; set; }
    public string Text { get; set; }

    public Document(string id, string author, string text) {
      Id = id;
      Author = author;
      Text = text;
    }

    public override string ToString() {
      return $"Id: {Id}, author: {Author}";
    }
  }
}