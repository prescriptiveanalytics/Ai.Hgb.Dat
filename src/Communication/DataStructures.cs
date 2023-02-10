namespace DCT.Communication {
  public struct HostAddress {
    public HostAddress(string server, int port) {
      Server = server;
      Port = port;
    }

    public string Server;
    public int Port;

    public override string ToString() {
      return $"{Server}:{Port}";
    }
  }

  public enum PayloadFormat {
    JSON,
    TOML,
    YAML,
    PROTOBUF
  }

  public enum SocketType {
    MQTT,
    APACHEKAFKA
  }

  public class MessageActionArgs : ICloneable {

    public string ClientId;
    public bool ProcessingFailed;
    public string ContentType;
    public byte[] Payload;
    public string Topic;
    public string ResponseTopic;

    public MessageActionArgs() { }

    public MessageActionArgs(string clientId, bool processingFailed, string contentType, byte[] payload, string topic, string responseTopic) {
      ClientId = clientId;
      ProcessingFailed = processingFailed;
      ContentType = contentType;
      Payload = payload.ToArray();
      Topic = topic;
      ResponseTopic = responseTopic;
    }

    public object Clone() {
      return new MessageActionArgs(ClientId, ProcessingFailed, ContentType, Payload, Topic, ResponseTopic);
    }
  }
}
