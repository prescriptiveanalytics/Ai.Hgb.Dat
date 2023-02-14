using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.Communication {
  public class MqttSocket : ISocket {
    public HostAddress Address => throw new NotImplementedException();

    public IEnumerable<string> Subscriptions => throw new NotImplementedException();

    public string BaseTopic { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public QualityOfServiceLevel QOSLevel { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event EventHandler<EventArgs<Message>> MessageReceived;

    public bool Connect() {
      throw new NotImplementedException();
    }

    public bool Disconnect() {
      throw new NotImplementedException();
    }

    public bool IsConnected() {
      throw new NotImplementedException();
    }

    public void Publish<T>(T message, string? topic = null) {
      throw new NotImplementedException();
    }

    public Task PublishAsync<T>(T message, string? topic = null) {
      throw new NotImplementedException();
    }

    public T Request<T>(string? topic = null) {
      throw new NotImplementedException();
    }

    public T1 Request<T1, T2>(T2 message, string? topic = null) {
      throw new NotImplementedException();
    }

    public Task<T> RequestAsync<T>(string? topic = null) {
      throw new NotImplementedException();
    }

    public Task<T1> RequestAsync<T1, T2>(T2 message, string? topic = null) {
      throw new NotImplementedException();
    }

    public void Subscribe(string? topic = null) {
      throw new NotImplementedException();
    }

    public void Subscribe(Action<Message, CancellationToken> handler, string? topic = null) {
      throw new NotImplementedException();
    }

    public void Unsubscribe(string? topic = null) {
      throw new NotImplementedException();
    }
  }
}
