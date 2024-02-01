using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Communication.Sockets;
using Ai.Hgb.Dat.Configuration;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Client;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Runtime.InteropServices;
using Ai.Hgb.Dat.Utils;

namespace Ai.Hgb.Dat.DemoApp {

  public class Program {

    static IPayloadConverter converter;
    static CountdownEvent ce;

    static void Main(string[] args) {
      var sw = new Stopwatch();
      sw.Start();

      //RunDemo_RabbitMQTest();
      //RunDemo_Mqtt_DocProducerConsumer();
      //RunDemo_Mqtt_DocRequestResponse();
      //RunDemo_Mqtt_DocRequestResponseMultipleClients();
      //RunDemo_ApacheKafka_ProducerConsumer();
      //RunDemo_ReadConfigurations();
      //RunDemo_ConfigurationMonitorBasedProducerConsumer();
      //RunDemo_DisposableBrokerSocket();
      //RunDemo_RegisterTypes();
      //RunDemo_MQTTandWS_StreamDoubleData();

      PerformanceTestSuite.RunIndividualTests();
      //PerformanceTestSuite.RunSuite();


      sw.Stop();
      //Console.WriteLine($"\n\nTime elapsed: {sw.Elapsed.TotalMilliseconds / 1000.0:f4} seconds");
      Console.WriteLine();
    }

    public static void RunDemo_Websockets() {

    }

    public static void RunDemo_RabbitMQTest() {
      converter = new JsonPayloadConverter();

      //SocketConfiguration pConfig = Parser.Parse<SocketConfiguration>(@"..\..\..\Configurations\ProducerConfig.yml");
      //ISocket producerOne = new MqttSocket(pConfig);
      //producerOne.Connect();
      //Task.Delay(1000).Wait();
      //for (int i = 0; i < 10; i++) {
      //  producerOne.Publish(new Document("doc" + i + 1, "jan", "foo bar"));
      //  Console.WriteLine("published");
      //  Task.Delay(2000).Wait();
      //}
      //Task.Delay(5000).Wait();
      //producerOne.Disconnect();

      var socket = new MqttFactory().CreateMqttClient();
      var options = new MqttClientOptionsBuilder()
        .WithClientId("lolo")
        //.WithCredentials("guest", "guest")
        //.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
        .WithTcpServer("127.0.0.1", 1883)
        //.WithKeepAlivePeriod(TimeSpan.FromSeconds(10))
        .WithCleanSession(true);

      var t = socket.ConnectAsync(options.Build());
      t.Wait();
      Console.WriteLine(t.Result.AssignedClientIdentifier);

      socket.SubscribeAsync("demo");
      socket.ApplicationMessageReceivedAsync += Socket_ApplicationMessageReceivedAsync;
      Task.Delay(1000).Wait();

      for (int i = 0; i < 10; i++) {
        var msg = "msg " + i + 1;
        var appMessage = new MqttApplicationMessageBuilder()
          .WithTopic("demo")
          //.WithResponseTopic("demo")
          .WithPayload(converter.Serialize(msg))
          //.WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
          .Build();

        try {
          socket.PublishAsync(appMessage);
        }
        catch (Exception ex) {
          Console.WriteLine(ex.Message.ToString());
        }
        Task.Delay(1000).Wait();
      }
      Task.Delay(10000).Wait();
      socket.DisconnectAsync().Wait();
    }

    private static Task Socket_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg) {
      var msg = arg.ApplicationMessage;
      Console.WriteLine("received: " + converter.Deserialize<string>(msg.Payload));
      return Task.CompletedTask;
    }

    public static void RunDemo_ConfigurationMonitorBasedProducerConsumer() {
      var cts = new CancellationTokenSource();
      int jobsPerProducer = 10;
      ce = new CountdownEvent(jobsPerProducer);

      var pMonitor = new Ai.Hgb.Dat.Configuration.Monitor<SocketConfiguration>();
      var cMonitor = new Ai.Hgb.Dat.Configuration.Monitor<SocketConfiguration>();
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

      using (var broker = new MqttBroker(pConfig.Broker).StartUp()) {
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

      int requestsPerClient = 100000;
      ce = new CountdownEvent(requestsPerClient);

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
      ISocket client = new MqttSocket("c1", "client", address, converter, null, pubOptions, reqOptions);

      //server.Connect();
      //client.Connect();

      //Thread.Sleep(1000);

      // do work
      var serverTask = Task.Factory.StartNew(() => ServeDocuments(server, cts.Token), cts.Token);
      Task.Delay(100).Wait();
      var clientTask = Task.Factory.StartNew(() => RequestDocuments(client, cts.Token, requestsPerClient), cts.Token);


      Console.WriteLine("Waiting for completion...");
      Task.WaitAll(new Task[] { clientTask });


      // tear down
      server.Disconnect();
      client.Disconnect();
      broker.TearDown();
    }

    public static void RunDemo_Mqtt_DocRequestResponseMultipleClients() {
      var cts = new CancellationTokenSource();

      int requestsPerClient = 50;
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
      Task.Delay(100).Wait();
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

      MqttBroker broker = new MqttBroker(address);
      //broker.StartUp();
      broker.StartUpWebsocket();

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
      Task.Delay(10000).Wait();
      broker.TearDown();
    }

    public static void RunDemo_RegisterTypes() {
      // setup client
      HostAddress address = new HostAddress("127.0.0.1", 1883);
      ISocket producer = new MqttSocket(id: "p1", name: "producerOne", address: address, converter: converter, connect: false);

      // register types
      producer.InterfaceStore.Register<ComplexDocument>("demo/docs", CommunicationMode.Publish);
      Console.WriteLine("\n\nRegistered Types: \n\n");
      Console.WriteLine(producer.InterfaceStore.GenerateSidlText());
      Console.WriteLine("\n\n");

    }

    public static void RunDemo_MQTTandWS_StreamDoubleData() {
      var cts = new CancellationTokenSource();
      var rnd = new Random();

      int count = 10000;
      int delay = 200;

      HostAddress address = new HostAddress("127.0.0.1", 1883);
      converter = new JsonPayloadConverter();

      MqttBroker broker = new MqttBroker(address);
      //broker.StartUp();
      broker.StartUpWebsocket();

      ISocket socket = new MqttSocket("socket1", "socket1", address, converter, connect: true);
      string group = Guid.NewGuid().ToString();

      for (int i = 0; i < count; i++) {
        // publish to dmon          
        var dtnow = DateTime.Now;
        var dt = String.Format("{0:yyyy-MM-dd-hh-mm-ss-fff}", dtnow); // YYYY-MM-DD-HH-mm-ss-SSS
        var msg_fit = new DmonItem("fit", group, 1, "Fit", rnd.NextGaussian_BoxMuller(10), dt, dt);
        var msg_sp = new DmonItem("sp", group, 1, "Selection Pressure", rnd.NextGaussian_BoxMuller(), dt, dt);
        var msg_lss = new DmonItem("lss", group, 1, "Local Search Success", rnd.NextGaussian_BoxMuller(), dt, dt);
        var msg_mr = new DmonItem("mr", group, 1, "Migration Rate", rnd.NextGaussian_BoxMuller(), dt, dt);
        var msg_gv = new DmonItem("gv", group, 1, "Genotype Variance", rnd.NextGaussian_BoxMuller(5), dt, dt);
        var msg_pv = new DmonItem("pv", group, 1, "Phenotype Variance", rnd.NextGaussian_BoxMuller(5), dt, dt);

        socket.Publish<DmonItem>("gae/run/fit", msg_fit);
        socket.Publish<DmonItem>("gae/run/sp", msg_sp);
        socket.Publish<DmonItem>("gae/run/lss", msg_lss);
        socket.Publish<DmonItem>("gae/run/mr", msg_mr);
        socket.Publish<DmonItem>("gae/run/gv", msg_gv);
        socket.Publish<DmonItem>("gae/run/pv", msg_pv);

        Task.Delay(delay).Wait();
      }

      Task.Delay(1000).Wait();
      socket.Disconnect();
      broker.TearDown();
    }

    private static void RequestDocuments(ISocket socket, CancellationToken token, int jobCount) {

      for (int i = 0; i < jobCount; i++) {
        var doc = socket.Request<Document>();
        //Console.WriteLine($"Client {socket.Configuration.Name} processing doc: {doc}");        
      }
    }

    private static void ServeDocuments(ISocket socket, CancellationToken token) {
      var rnd = new Random();
      var o = socket.Configuration.DefaultRequestOptions;
      var count = 0;

      socket.Subscribe(o.GetRequestSubscriptionOptions(),
        (IMessage docReq, CancellationToken token) =>
        {
          count = Interlocked.Increment(ref count);
          var doc = new Document(socket.Configuration.Id + "-" + count, "server", "lorem ipsum dolor");
          //Task.Delay(500 + rnd.Next(1000)).Wait();
          //Console.WriteLine($"Produced doc: {doc}");
          var pOpt = new PublicationOptions(docReq.ResponseTopic, "", QualityOfServiceLevel.ExactlyOnce);
          socket.Publish(pOpt, doc);
        }, token);
    }

    private static void ProduceDocuments(ISocket socket, int jobCount) {
      var rnd = new Random();
      var t = Task.Factory.StartNew(() =>
      {
        for (int i = 0; i < jobCount; i++) {
          var doc = new Document(socket.Configuration.Id + "-" + (i + 1), socket.Configuration.Name, "lorem ipsum dolor");
          Task.Delay(5000 + rnd.Next(1000)).Wait();
          Console.WriteLine($"Produced doc: {doc}");
          socket.Publish(doc);
        }
      });

      t.Wait();
    }

    private static void ProcessDocument(IMessage docMsg, CancellationToken token) {
      Document doc;
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
}