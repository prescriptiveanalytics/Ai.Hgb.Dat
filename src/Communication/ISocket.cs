namespace DCT.Communication {
  public interface ISocket {

    HostAddress Address { get; }

    IEnumerable<string> Subscriptions { get; }
    
    bool Connect();

    bool Disconnect();

    bool IsConnected();

    void Subscribe(string topic);

    void Unsubscribe(string topic);

    void SendMessage<T>(string topic, T message, string responseTopic = null);
  }
}