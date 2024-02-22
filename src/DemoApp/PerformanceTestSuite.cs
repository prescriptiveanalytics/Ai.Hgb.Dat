using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Communication.Sockets;
using Ai.Hgb.Dat.Configuration;
using Ai.Hgb.Dat.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ai.Hgb.Dat.DemoApp {
  public class PerformanceTestSuite {

    public static void RunSuite() {
      string path = @"C:\Users\P41608\Desktop\ism_experiments\results.csv";
      IPayloadConverter converter;

      int repetitions = 10;
      var runCounts = new List<int>() { 1, 10, 100, 1000, 10000, 100000 }; // 1, 10, 100, 1000, 10000, 100000, 1000000
      var arrayLengths = new List<int>() { 1000 }; // 1, 10, 100, 1000, 10000, 100000, 1000000
      var experiments = new List<Experiment>();
      //converter = new JsonPayloadConverter();
      converter = new MemoryPackPayloadConverter();
      int expCount = 0;

      Console.WriteLine("Run Performance Experiments:\n");
      for (int i = 0; i < arrayLengths.Count; i++) {
        for(int j = 0; j < runCounts.Count; j++) {

          Console.WriteLine("mqtt-pubsub");
          var expMqttPubsub = RunTest_MQTTSocket_PublishSubscribe(repetitions, runCounts[j], arrayLengths[i], converter);
          expMqttPubsub.Nr = ++expCount;
          Console.WriteLine(expMqttPubsub);
          Task.Delay(1000).Wait();

          Console.WriteLine("smem-pubsub");
          var expShamPubsub = RunTest_SharedMemorySocket_PublishSubscribe(repetitions, runCounts[j], arrayLengths[i], converter);
          expShamPubsub.Nr = ++expCount;
          Console.WriteLine(expShamPubsub);
          Task.Delay(1000).Wait();

          Console.WriteLine("mqtt-reqres");
          var expMqttReqres = RunTest_MQTTSocket_RequestResponse(repetitions, runCounts[j], arrayLengths[i], converter);
          expMqttReqres.Nr = ++expCount;
          Console.WriteLine(expMqttReqres);
          Task.Delay(1000).Wait();

          Console.WriteLine("smem-reqres");
          var expShamReqres = RunTest_SharedMemorySocket_RequestResponse(repetitions, runCounts[j], arrayLengths[i], converter);
          expShamReqres.Nr = ++expCount;
          Console.WriteLine(expShamReqres);
          Task.Delay(1000).Wait();

          if (i == 0 && j == 0) {
            //Console.WriteLine(expShamReqres.GetTitleline());
            using (var sw = new StreamWriter(path, false)) sw.WriteLine(expShamReqres.GetCSVTitleline());
          }

          experiments.Add(expMqttPubsub);
          experiments.Add(expShamPubsub);
          experiments.Add(expMqttReqres);
          experiments.Add(expShamReqres);

          using (var sw = new StreamWriter(path, true)) {
            sw.WriteLine(expMqttPubsub.ToCSVString());
            sw.WriteLine(expShamPubsub.ToCSVString());
            sw.WriteLine(expMqttReqres.ToCSVString());
            sw.WriteLine(expShamReqres.ToCSVString());
          }
        }
      }
      Console.WriteLine("\n\n End of Experiments");

      
    }

    public static void RunIndividualTests() {
      var runCount = 1000;
      var arrayLength = 1000;
      IPayloadConverter converter;

      // Test 1
      //converter = new JsonPayloadConverter();
      //var exp1 = RunTest_MQTTSocket(runCount, arrayLength, converter);
      //var exp2 = RunTest_SharedMemorySocket(runCount, arrayLength, converter);

      //Console.WriteLine(exp1.GetTitleline());
      //Console.WriteLine(exp1);
      //Console.WriteLine(exp2);

      // Test 2
      //converter = new JsonPayloadConverter();
      //var exp1 = RunTest_SharedMemorySocket(runCount, arrayLength, converter);
      //converter = new MemoryPackPayloadConverter();
      //var exp2 = RunTest_SharedMemorySocket(runCount, arrayLength, converter);

      //Console.WriteLine(exp1.GetTitleline());
      //Console.WriteLine(exp1);
      //Console.WriteLine(exp2);

      // Test 3
      //converter = new MemoryPackPayloadConverter();
      //converter = new JsonPayloadConverter();
      //var exp1 = RunTest_MQTTSocket_RequestResponse(runCount, arrayLength, converter);
      //Console.WriteLine(exp1.GetTitleline());
      //Console.WriteLine(exp1);

      // Test 4
      converter = new JsonPayloadConverter();
      //converter = new MemoryPackPayloadConverter();
      var exp1 = RunTest_MQTTSocket_PublishSubscribe(1, runCount, arrayLength, converter);
      Console.WriteLine(exp1.GetTitleline());
      Console.WriteLine(exp1);

      // Test 5
      //converter = new JsonPayloadConverter();
      //var exp1 = RunTest_SharedMemorySocket_PublishSubscribe(1, runCount, arrayLength, converter);
      //Console.WriteLine(exp1.GetTitleline());
      //Console.WriteLine(exp1);
    }

    #region SharedMemory request-response
    public static Experiment RunTest_SharedMemorySocket_RequestResponse(int repetitions, int runCount, int arrayLength, IPayloadConverter converter) {
      var payload = new DoubleTypedArray(arrayLength);
      for (int a = 0; a < payload.Value.Length; a++) payload.Value[a] = 0.1;
      int payloadSize = converter.Serialize(payload).Length;

      var results = new List<double>();
      for (int i = 0; i < repetitions; i++) {
        results.Add(RunTest_MQTTSocket_PublishSubscribe(runCount, converter, payload));
      }

      return new Experiment(0, "Shared Memory req/res", "smem", "reqres", "json", payloadSize, runCount, repetitions, results.Sum(), results.Average(), results.Median(), results.Min(), results.Max(), results.StandardDeviation());
    }

    public static double RunTest_SharedMemorySocket_RequestResponse(int runCount, IPayloadConverter converter, DoubleTypedArray payload) {
      var cts = new CancellationTokenSource();

      var socket1 = new SharedMemorySocket("server", converter);
      var socket2 = new SharedMemorySocket("client", converter);

      string topic = "gae-solutions-" + Guid.NewGuid().ToString();
      socket1.Respond<DoubleTypedArray, DoubleTypedArray>(topic, ProcessRequest, cts.Token);

      Console.WriteLine("Waiting for completion...");
      Stopwatch sw = new Stopwatch();
      sw.Start();
      // BEGIN OF EXPERIMENT

      for (int i = 0; i < runCount; i++) {
        var popNew = socket2.Request<DoubleTypedArray, DoubleTypedArray>(topic, payload, cts.Token).Result;
        //Console.WriteLine($"{i}: {popNew.Fits}");
      }

      // END OF EXPERIMENT
      sw.Stop();
      Console.WriteLine("Completed");

      socket1.Close();
      socket2.Close();
      Task.Delay(100).Wait();

      Console.WriteLine("Returning");
      return sw.Elapsed.TotalMilliseconds;
    }

    private static DoubleTypedArray ProcessRequest(DoubleTypedArray candidate, CancellationToken token) {
      candidate.Value[0] = 0.1;
      return candidate;
    }

    #endregion SharedMemory request-response

    #region SharedMemory publish-subscribe
    public static Experiment RunTest_SharedMemorySocket_PublishSubscribe(int repetitions, int runCount, int arrayLength, IPayloadConverter converter) {
      var payload = new DoubleTypedArray(arrayLength);
      for (int a = 0; a < payload.Value.Length; a++) payload.Value[a] = 0.1;
      int payloadSize = converter.Serialize(payload).Length;

      var results = new List<double>();
      for (int i = 0; i < repetitions; i++) {
        results.Add(RunTest_SharedMemorySocket_PublishSubscribe(runCount, converter, payload));
      }

      return new Experiment(0, "Shared Memory pub/sub", "smem", "pub/sub", "json", payloadSize, runCount, repetitions, results.Sum(), results.Average(), results.Median(), results.Min(), results.Max(), results.StandardDeviation());
    }

    public static double RunTest_SharedMemorySocket_PublishSubscribe(int runCount, IPayloadConverter converter, DoubleTypedArray payload) {
      var cts = new CancellationTokenSource();
      ce = new CountdownEvent(runCount);

      var socket1 = new SharedMemorySocket("producer", converter);
      var socket2 = new SharedMemorySocket("consumer", converter);
      string topic = "gae-solutions-" + Guid.NewGuid().ToString();
      socket2.Subscribe<DoubleTypedArray>(topic, ConsumeData, cts.Token);
      Task.Delay(100).Wait();

      Console.WriteLine("Waiting for completion...");
      Stopwatch sw = new Stopwatch();
      sw.Start();
      // BEGIN OF EXPERIMENT

      for (int i = 0; i < runCount; i++) {
        socket1.Publish(topic, payload, cts.Token);
      }

      ce.Wait();
      // END OF EXPERIMENT
      sw.Stop();
      //Console.WriteLine($"\n\nTime elapsed: {sw.Elapsed.TotalMilliseconds:f2} milliseconds");
      Console.WriteLine("Completed");

      socket1.Close();
      socket2.Close();
      Task.Delay(100).Wait();

      Console.WriteLine("Returning");
      return sw.Elapsed.TotalMilliseconds;
    }

    private static void ConsumeData(DoubleTypedArray msg, CancellationToken token) {
      //Task.Delay(1000).Wait();
      //Console.WriteLine("result: " + msg.Value[0]);
      ce.Signal();
    }
    #endregion SharedMemory publish-subscribe

    #region MQTT request-response

    public static Experiment RunTest_MQTTSocket_RequestResponse(int repetitions, int runCount, int arrayLength, IPayloadConverter converter) {
      var payload = new DoubleTypedArray(arrayLength);
      for (int a = 0; a < payload.Value.Length; a++) payload.Value[a] = 0.1;
      int payloadSize = converter.Serialize(payload).Length;

      var results = new List<double>();
      for (int i = 0; i < repetitions; i++) {
        results.Add(RunTest_MQTTSocket_RequestResponse(runCount, converter, payload));
      }

      return new Experiment(0, "MQTT req/res", "mqtt", "reqres", "json", payloadSize, runCount, repetitions, results.Sum(), results.Average(), results.Median(), results.Min(), results.Max(), results.StandardDeviation());
    }

    public static double RunTest_MQTTSocket_RequestResponse(int runCount, IPayloadConverter converter, DoubleTypedArray payload) {
      var cts = new CancellationTokenSource();

      HostAddress address = new HostAddress("127.0.0.1", 1883);

      IBroker broker = new MqttBroker(address);
      broker.StartUp();
      Task.Delay(1000).Wait(); ;

      string pubsubTopic = "demoapp/pop";
      string respTopic = "demoapp/responses";
      string reqTopic = "demoapp/pop";
      var pubOptions = new PublicationOptions(pubsubTopic, respTopic, QualityOfServiceLevel.ExactlyOnce);
      var subOptions = new SubscriptionOptions(pubsubTopic, QualityOfServiceLevel.ExactlyOnce);
      var reqOptions = new RequestOptions(reqTopic, respTopic, true);

      ISocket server = new MqttSocket("s1", "server", address, converter, null, pubOptions, reqOptions);
      ISocket client = new MqttSocket("c1", "client", address, converter, null, pubOptions, reqOptions);


      Console.WriteLine("Waiting for completion...");
      Stopwatch sw = new Stopwatch();
      sw.Start();
      // BEGIN OF EXPERIMENT

      // do work
      var serverTask = Task.Factory.StartNew(() => ServeData(server, converter, cts.Token), cts.Token);
      Task.Delay(100).Wait();
      var clientTask = Task.Factory.StartNew(() => RequestData(client, cts.Token, runCount, payload), cts.Token);


      
      Task.WaitAll(new Task[] { clientTask });

      // END OF EXPERIMENT
      sw.Stop();
      Console.WriteLine("Completed");

      // tear down
      server.Disconnect();
      client.Disconnect();
      Task.Delay(1000).Wait();
      broker.TearDown();
      Task.Delay(1000);


      Console.WriteLine("Returning");
      return sw.Elapsed.TotalMilliseconds;
    }

    private static void ServeData(ISocket socket, IPayloadConverter converter, CancellationToken token) {
      var rnd = new Random();
      var o = socket.Configuration.DefaultRequestOptions;

      socket.Subscribe<DoubleTypedArray>(o.GetRequestSubscriptionOptions(),
        (IMessage popMsg, CancellationToken token) => {
          DoubleTypedArray pop;
          if (popMsg.Content != null && popMsg.Content is DoubleTypedArray) pop = (DoubleTypedArray)popMsg.Content;
          else pop = converter.Deserialize<DoubleTypedArray>(popMsg.Payload);

          // modify
          pop.Value[0] = 2.0;

          socket.Publish(popMsg.ResponseTopic, pop);
        }, token);
    }

    private static void RequestData(ISocket socket, CancellationToken token, int jobCount, DoubleTypedArray candidate) {
      for (int i = 0; i < jobCount; i++) {
        var newpop = socket.Request<DoubleTypedArray, DoubleTypedArray>(candidate);
      }
    }

    #endregion MQTT request-response

    #region MQTT publish-subscribe

    static CountdownEvent ce;
    public static Experiment RunTest_MQTTSocket_PublishSubscribe(int repetitions, int runCount, int arrayLength, IPayloadConverter converter) {
      // prepare payload
      var payload = new DoubleTypedArray(arrayLength);
      for (int a = 0; a < payload.Value.Length; a++) payload.Value[a] = 0.1;
      int payloadSize = converter.Serialize(payload).Length;

      var results = new List<double>();
      for(int i = 0; i < repetitions; i++) {
        results.Add(RunTest_MQTTSocket_PublishSubscribe(runCount, converter, payload));
      }

      return new Experiment(0, "MQTT pub/sub", "mqtt", "pub/sub", "json", payloadSize, runCount, repetitions, results.Sum(), results.Average(), results.Median(), results.Min(), results.Max(), results.StandardDeviation());
    }

    public static double RunTest_MQTTSocket_PublishSubscribe(int runCount, IPayloadConverter converter, DoubleTypedArray payload) {
      var cts = new CancellationTokenSource();
      ce = new CountdownEvent(runCount);

      HostAddress address = new HostAddress("127.0.0.1", 1883);
      MqttBroker broker = new MqttBroker(address);
      broker.StartUp();
      Task.Delay(1000).Wait();

      string pubsubTopic = "demoapp/performance/mqtt/pubsubtest";
      var pubOptions = new PublicationOptions(pubsubTopic, pubsubTopic, QualityOfServiceLevel.ExactlyOnce);
      var subOptions = new SubscriptionOptions(pubsubTopic, QualityOfServiceLevel.ExactlyOnce);

      ISocket producer, consumer;
      producer = new MqttSocket("p1", "producer", address, converter, defPubOptions: pubOptions, connect: true);
      consumer = new MqttSocket("c1", "consumer", address, converter, defSubOptions: subOptions, connect: true);
      Task.Run(() => consumer.Subscribe<DoubleTypedArray>(ConsumeData, cts.Token)); // TODO: change order, move up!
      Task.Delay(100).Wait();

      Console.WriteLine("Waiting for completion...");
      Stopwatch sw = new Stopwatch();
      sw.Start();
      // BEGIN OF EXPERIMENT

      Task.Run(() => ProduceData(producer, runCount, payload));

      // wait for completion
      ce.Wait();

      // END OF EXPERIMENT
      sw.Stop();
      Console.WriteLine("Completed");

      producer.Disconnect();
      consumer.Disconnect();
      Task.Delay(100).Wait();
      broker.TearDown();
      Task.Delay(1000).Wait();

      Console.WriteLine("Returning");
      return sw.Elapsed.TotalMilliseconds;
    }

    private static void ProduceData(ISocket socket, int runCount, DoubleTypedArray payload) {
      for(int i = 0; i < runCount; i++) {
        socket.Publish(payload);
      }
    }

    private static void ConsumeData(IMessage msg, CancellationToken token) {
      var data = (DoubleTypedArray)msg.Content;
      ce.Signal();
    }

    #endregion MQTT publish-subscribe

    public class Experiment {
      public int Nr { get; set; }
      public string Name { get; set; }
      public string Category1 { get; set; }
      public string Category2 { get; set; }
      public string Category3 { get; set; }
      public int PayloadSize { get; set; }
      public int RunCount { get; set; }
      public int Repetitions { get; set; }
      public double Runtime { get; set; }

      public double Mean { get; set; }
      public double Median { get; set; }
      public double Min { get; set; }
      public double Max { get; set; }
      public double StdDev { get; set; }

      public Experiment() { }  
      public Experiment(int nr, string name, string category1, string category2, string category3, int payloadSize, int runCount, int repetitions, double runtime, double mean, double median, double min, double max, double stddev) {
        Nr = nr;
        Name = name;
        Category1 = category1;
        Category2 = category2;
        Category3 = category3;
        PayloadSize = payloadSize;
        RunCount = runCount;

        Repetitions= repetitions;
        Runtime = runtime;
        Mean = mean;
        Median = median;
        Min = min;
        Max = max;          
        StdDev = stddev;
      }

      public string GetTitleline() {
        return "Nr   Name\tCat1/Cat2/Cat3\t\tSize\tCount\tmRuntime";
      }

      public override string ToString() {
        return $"{Nr:0000} {Name}\t{Category1}/{Category2}/{Category3}\t{PayloadSize}\t{RunCount}\t{Mean:00000000}";
      }

      public string GetCSVTitleline() {
        return "Nr;Name;Socket;MessagingPattern;PayloadConverter;MessageSize;RunCount;Repetitions;Runtime;Mean;Median;Min;Max;StdDev";
      }

      public string ToCSVString() {
        return $"{Nr};{Name};{Category1};{Category2};{Category3};{PayloadSize};{RunCount};{Repetitions};{Runtime};{Mean};{Median};{Min};{Max};{StdDev}";
      }
    }
  }
}
