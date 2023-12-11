using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace DAT.Communication.Sockets {
  public class SharedMemorySocket {

    public string Id { get => id; set => id = value; }
    public int BufferCapacity {  get => bufferCapacity; set => bufferCapacity = value; }


    private string id;
    private IPayloadConverter converter;
    private bool connected;
    private CancellationTokenSource cts;
    private int bufferCapacity;

    private Dictionary<string, SharedMemoryServer> servers;
    private Dictionary<string, SharedMemoryClient> clients;

    private Dictionary<string, SharedMemoryProducer> producers;
    private Dictionary<string, SharedMemoryConsumer> consumers;
    private Dictionary<string, SharedMemoryBuffer> buffers;

    public SharedMemorySocket(string id, IPayloadConverter converter) {
      this.id = id;
      this.converter = converter;
      cts = new CancellationTokenSource();
      connected = true;
      bufferCapacity = 100;

      servers = new Dictionary<string, SharedMemoryServer>();
      clients = new Dictionary<string, SharedMemoryClient>();

      producers = new Dictionary<string, SharedMemoryProducer>();
      consumers = new Dictionary<string, SharedMemoryConsumer>();
      buffers = new Dictionary<string, SharedMemoryBuffer>();

    }

    public object Clone() {
      return new SharedMemorySocket(id, converter);
    }

    public bool IsConnected { get { return connected; } }

    public SharedMemorySocket Connect() {
      return this;
    }

    public void Close() {
      cts.Cancel();

      foreach (var server in servers.Values) {
        server.Dispose();
      }

      foreach (var client in clients.Values) {
        client.Dispose();
      }

      foreach (var producer in producers.Values) producer.Dispose();
      foreach (var consumer in consumers.Values) consumer.Dispose();
      foreach (var buffer in buffers.Values) buffer.Dispose();
    }

    public void Respond<T1, T2>(string topic, Func<T1, CancellationToken, T2> handler, CancellationToken? token = null) {
      CancellationToken ct = (token != null && token.HasValue) ? token.Value : cts.Token;

      if (!servers.ContainsKey(topic)) {
        var server = new SharedMemoryServer(topic, converter);
        servers.Add(topic, server);
        server.Initialize();
        server.Run(handler, ct);
      }
    }

    public async Task<T2> Request<T1, T2>(string topic, T1 message, CancellationToken? token = null) {
      CancellationToken ct = (token != null && token.HasValue) ? token.Value : cts.Token;
      SharedMemoryClient client;
      if (!clients.ContainsKey(topic)) {
        client = new SharedMemoryClient(topic, converter);
        clients.Add(topic, client);
        client.Connect();
      }
      else {
        client = clients[topic];
      }

      var result =  await client.Run<T1, T2>(message, ct);
      return result;  
    }

    // 1. register subscriber, setup buffer
    public Task Subscribe<T>(string topic, Action<T, CancellationToken> handler, CancellationToken? token = null) {
      CancellationToken ct = (token != null && token.HasValue) ? token.Value : cts.Token;
      SharedMemoryConsumer consumer;
      if (!consumers.ContainsKey(topic)) {
        var buffer = new SharedMemoryBuffer(topic, bufferCapacity, converter, true);
        buffers.Add(topic, buffer);
        consumer = new SharedMemoryConsumer(topic, buffer);
        consumers.Add(topic, consumer);
      } else {
        consumer = consumers[topic];
      }

      return consumer.Run(handler, ct);
    }

    // 2. publish if there is already 1+ subscriber and thus, a buffer to write to
    public bool Publish<T>(string topic, T message, CancellationToken? token = null) {
      CancellationToken ct = (token != null && token.HasValue) ? token.Value : cts.Token;
      SharedMemoryProducer producer;
      if(!producers.ContainsKey(topic)) {
        SharedMemoryBuffer buffer;
        if(!buffers.ContainsKey(topic)) {
          try {
            buffer = new SharedMemoryBuffer(topic, bufferCapacity, converter, false);
            buffers.Add(topic, buffer);
          }
          catch (Exception ex) {
            return false;
          }
        } else {
          buffer = buffers[topic];
        }
        producer = new SharedMemoryProducer(topic, buffer);
        producers.Add(topic, producer);
      } else {
        producer = producers[topic];
      }

      producer.Run(message, ct);
      return true;
    }
  }

  #region RequestResponse

  public class SharedMemoryClient {

    public string Id { get => id; set => id = value; }

    private string id;
    private IPayloadConverter converter;

    private string semId_ticket;
    private string semId_request;
    private string semId_response;
    private Semaphore sem_ticket;
    private Semaphore sem_request;
    private Semaphore sem_response;

    private string mmfId;
    private long mmfCapacity;

    private MemoryMappedFile mmf;
    private MemoryMappedViewStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;

    public SharedMemoryClient(string id, IPayloadConverter converter) {
      this.id = id;
      this.converter = converter;

      semId_ticket = $"sms_ticket_{id}";
      semId_request = $"sms_request_{id}";
      semId_response = $"sms_response_{id}";


      mmfId = $"mmf_{id}";
      mmfCapacity = 0x20000000;
    }

    public bool Connect() {
      try {
        sem_ticket = Semaphore.OpenExisting(semId_ticket);
        sem_request = Semaphore.OpenExisting(semId_request);
        sem_response = Semaphore.OpenExisting(semId_response);

        mmf = MemoryMappedFile.OpenExisting(mmfId);
        stream = mmf.CreateViewStream();
        reader = new BinaryReader(stream);
        writer = new BinaryWriter(stream);

        return true;
      }
      catch {
        return false;
      }
    }

    public Task<T2> Run<T1, T2>(T1 message, CancellationToken token) {
      return Task.Run(() =>
      {
        sem_ticket.WaitOne();

        // write request message
        stream.Position = 0;
        var newBytes = converter.Serialize(message);
        writer.Write(newBytes.Length);
        writer.Write(newBytes);

        sem_request.Release();

        // read response
        sem_response.WaitOne();

        // read response message
        stream.Position = 0;
        int length = reader.ReadInt32();
        var bytes = reader.ReadBytes(length);
        var responseMessage = converter.Deserialize<T2>(bytes);

        sem_ticket.Release();

        return responseMessage;
      }, token);
    }

    public void Dispose() {
      sem_ticket.Dispose();
      sem_request.Dispose();
      sem_response.Dispose();

      reader.Dispose();
      writer.Dispose();
      stream.Dispose();
      mmf.Dispose();
    }
  }

  public class SharedMemoryServer {

    public string Id { get => id; set => id = value; }

    private string id;
    private IPayloadConverter converter;

    private string semId_ticket;
    private string semId_request;
    private string semId_response;
    private Semaphore sem_ticket;
    private Semaphore sem_request;
    private Semaphore sem_response;

    private string mmfId;
    private long mmfCapacity;

    private MemoryMappedFile mmf;
    private MemoryMappedViewStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;

    private Task task;

    public SharedMemoryServer(string id, IPayloadConverter converter) {
      this.id = id;
      this.converter = converter;

      semId_ticket = $"sms_ticket_{id}";
      semId_request = $"sms_request_{id}";
      semId_response = $"sms_response_{id}";

      mmfId = $"mmf_{id}";
      mmfCapacity = 0x20000000;
    }

    public bool Initialize() {
      try {
        sem_ticket = new Semaphore(1, 1, semId_ticket);
        sem_request = new Semaphore(0, 1, semId_request);
        sem_response = new Semaphore(0, 1, semId_response);

        mmf = MemoryMappedFile.CreateNew(mmfId, mmfCapacity);
        stream = mmf.CreateViewStream();
        reader = new BinaryReader(stream);
        writer = new BinaryWriter(stream);

        return true;
      }
      catch {
        return false;
      }
    }

    public void Run<T1, T2>(Func<T1, CancellationToken, T2> handler, CancellationToken token) {
      task = Task.Run(() =>
      {
        try {
          while (!token.IsCancellationRequested) {
            // request
            sem_request.WaitOne();
            if (token.IsCancellationRequested) return;
            stream.Position = 0;
            int length = reader.ReadInt32();
            var bytes = reader.ReadBytes(length);

            // perform action
            T1 msg = converter.Deserialize<T1>(bytes);
            T2 responseMsg = handler(msg, token);

            // response
            stream.Position = 0;
            var newBytes = converter.Serialize(responseMsg);
            writer.Write(newBytes.Length);
            writer.Write(newBytes);

            if (token.IsCancellationRequested) return;
            sem_response.Release();
          }
        }
        catch { }
      }, token);
    }

    public void Dispose() {
      sem_ticket.Dispose();
      sem_request.Dispose();
      sem_response.Dispose();

      reader.Dispose();
      writer.Dispose();
      stream.Dispose();
      mmf.Dispose();

      task = null;
      //task.Dispose();
    }
  }

  #endregion RequestResponse

  #region PublishSubscribe

  public class SharedMemoryProducer {
    public string Id { get => id; set => id = value; }

    private string id;
    private SharedMemoryBuffer buffer;

    public SharedMemoryProducer(string id, SharedMemoryBuffer buffer) {
      this.id = id;
      this.buffer = buffer;
    }

    public Task Run<T>(T message, CancellationToken token) {
      return Task.Run(() => {
        buffer.WaitToWrite();
        buffer.Write<T>(message);
        buffer.ReleaseToRead();
      }, token);
    }

    public void Dispose() {
      
    }
  }

  public class SharedMemoryConsumer {
    public string Id { get => id; set => id = value; }

    private string id;
    private SharedMemoryBuffer buffer;
    private Task consumerTask;

    public SharedMemoryConsumer(string id, SharedMemoryBuffer buffer) {
      this.id = id;
      this.buffer = buffer;
    }

    public Task Run<T>(Action<T, CancellationToken> handler, CancellationToken token) {
      consumerTask = Task.Run(() => {
        while(true) {
          buffer.WaitToRead();
          var msg = buffer.Read<T>();
          buffer.ReleaseToWrite();
          handler(msg, token);
        }
      }, token);
      return consumerTask;
    }

    public void Dispose() {
      
    }
  }

  public class SharedMemoryBuffer {
    private string id;
    private int capacity;
    private int writerIndex;
    private int readerIndex;

    private object locker;

    private string semId_full;
    private string semId_empty;
    private Semaphore sem_full;
    private Semaphore sem_empty;
    private IPayloadConverter converter;

    public List<SharedMemoryBufferItem> Items { get; set; }

    public SharedMemoryBuffer(string id, int capacity, IPayloadConverter converter, bool create) {
      this.id = id;
      this.capacity = capacity;
      this.converter = converter;
      writerIndex = 0;
      readerIndex = 0;

      locker = new object();

      Items = new List<SharedMemoryBufferItem>(capacity);
      for (int i = 0; i < capacity; i++) {
        Items.Add(new SharedMemoryBufferItem($"{id}_{i}", create));
      }

      semId_full = $"semfull_{id}";
      semId_empty = $"semempty_{id}";
      if(create) {
        sem_empty = new Semaphore(capacity, capacity, semId_empty);
        sem_full = new Semaphore(0, capacity, semId_full);
      } else {
        sem_empty = Semaphore.OpenExisting(semId_empty);
        sem_full = Semaphore.OpenExisting(semId_full);
      }

    }

    public void WaitToWrite() {
      sem_empty.WaitOne();
    }

    public void WaitToRead() {
      sem_full.WaitOne();
    }

    public void ReleaseToWrite() {
      sem_empty.Release();
    }

    public void ReleaseToRead() {
      sem_full.Release();
    }

    public void Write<T>(T message) {
      var bytes = converter.Serialize(message);

      // V1
      //int localWriterIndex;
      //lock (locker) {
      //  localWriterIndex = writerIndex;
      //  writerIndex = (writerIndex + 1) % capacity;
      //}
      //Items[localWriterIndex].Write(bytes);

      // V2
      lock(locker) {
        writerIndex = (writerIndex + 1) % capacity;
        Items[writerIndex].Write(bytes);
      }
    }

    public T Read<T>() {
      // V1
      //int localReaderIndex;
      //lock (locker) {
      //  localReaderIndex = readerIndex;
      //  readerIndex = (readerIndex + 1) % capacity;
      //}
      //var bytes = Items[localReaderIndex].Read();

      // V2
      byte[] bytes;
      lock (locker) {
        readerIndex = (readerIndex + 1) % capacity;
        bytes = Items[readerIndex].Read();
      }

      return converter.Deserialize<T>(bytes);
    }

    public void Dispose() {
      foreach (var item in Items) item.Dispose();
    }
  }

  public class SharedMemoryBufferItem {
    private string id;
    private string mmfId;
    private long mmfCapacity;

    private MemoryMappedFile mmf;
    private MemoryMappedViewStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;

    public SharedMemoryBufferItem(string id, bool create) {
      this.id = id;

      mmfId = $"mmf_{id}";
      mmfCapacity = 0x20000000;

      if (create) {
        mmf = MemoryMappedFile.CreateNew(mmfId, mmfCapacity);
      }
      else {
        mmf = MemoryMappedFile.OpenExisting(mmfId);
      }

      stream = mmf.CreateViewStream();
      reader = new BinaryReader(stream);
      writer = new BinaryWriter(stream);
    }

    public byte[] Read() {
      stream.Position = 0;
      int length = reader.ReadInt32();
      return reader.ReadBytes(length);
    }

    public void Write(byte[] item) {
      stream.Position = 0;
      writer.Write(item.Length);
      writer.Write(item);
    }

    public void Dispose() {
      reader.Dispose();
      writer.Dispose();
      stream.Dispose();
      mmf.Dispose();
    }
  }

  #endregion PublishSubscribe
}
