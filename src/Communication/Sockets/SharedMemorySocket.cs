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

    private string id;
    private IPayloadConverter converter;
    private bool connected;
    private CancellationTokenSource cts;

    private Dictionary<string, SharedMemoryServer> servers;
    private Dictionary<string, SharedMemoryClient> clients;

    public SharedMemorySocket(string id, IPayloadConverter converter) {
      this.id = id;
      this.converter = converter;
      cts = new CancellationTokenSource();
      connected = true;

      servers = new Dictionary<string, SharedMemoryServer>();
      clients = new Dictionary<string, SharedMemoryClient>();
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
    }

    public void Subscribe<T1, T2>(string topic, Func<T1, CancellationToken, T2> handler, CancellationToken? token = null) {
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
  }

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
}
