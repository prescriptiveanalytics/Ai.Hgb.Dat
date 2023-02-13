namespace DCT.Communication {
  public interface ISocket {

    HostAddress Address { get; }

    IEnumerable<string> Subscriptions { get; }

    event EventHandler<EventArgs<Message>> MessageReceived;
    
    bool Connect();

    bool Disconnect();

    bool IsConnected();

    void Subscribe(string topic);

    void Unsubscribe(string topic);

    void Send<T>(string topic, T message, string responseTopic = null);
  }
}