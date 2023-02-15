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
    PROTOBUF,
    SIDL
  }

  public enum SocketType {
    MQTT,
    APACHEKAFKA
  }

  public enum QualityOfServiceLevel {
    AtMostOnce,
    AtLeastOnce,
    ExactlyOnce
  }

  public class Message : ICloneable {

    public string ClientId;    
    public string ContentType;
    public byte[] Payload;
    public string Topic;
    public string ResponseTopic;

    private Message() { }

    public Message(string clientId, string contentType, byte[] payload, string topic, string responseTopic) {
      ClientId = clientId;      
      ContentType = contentType;
      Payload = payload.ToArray();
      Topic = topic;
      ResponseTopic = responseTopic;
    }

    public object Clone() {
      return new Message(ClientId, ContentType, Payload, Topic, ResponseTopic);
    }
  }

  public class EventArgs<T> : EventArgs {
    public T Value { get; private set; }

    public EventArgs(T value) {
      Value = value;
    }
  }

  public class ActionItem : ICloneable {

    public Action<Message, CancellationToken> Action;
    public CancellationToken Token;

    public ActionItem(Action<Message, CancellationToken> action, CancellationToken token) {
      Action = action;
      Token = token;
    }

    public object Clone() {
      return new ActionItem(Action, Token); 
    }
  }

  public class SubscriptionOptions : ICloneable {

    public string Topic;
    public QualityOfServiceLevel QosLevel;    

    public SubscriptionOptions(string topic, QualityOfServiceLevel qosLevel) {
      Topic = topic;
      QosLevel = qosLevel;
    }

    public object Clone() {
      return new SubscriptionOptions(Topic, QosLevel);
    }
  }

  public class PublicationOptions : ICloneable {

    public string Topic;
    public string ResponseTopic;
    public QualityOfServiceLevel QosLevel;

    public PublicationOptions(string topic, string responseTopic, QualityOfServiceLevel qosLevel) {
      Topic = topic;
      ResponseTopic = responseTopic;
      QosLevel = qosLevel;
    }

    public object Clone() {
      return new PublicationOptions(Topic, ResponseTopic, QosLevel);
    }
  }

  public class RequestOptions : ICloneable {

    public string Topic;
    public string ResponseTopic;
    public bool GenerateResponseTopicPostfix;    

    public RequestOptions(string topic, string responseTopic, bool generateResponseTopicPostfix) {
      Topic = topic;
      ResponseTopic = responseTopic;
      GenerateResponseTopicPostfix = generateResponseTopicPostfix;      
    }

    public object Clone() {
      return new RequestOptions(Topic, ResponseTopic, GenerateResponseTopicPostfix);
    }

    public SubscriptionOptions GetSubscriptionOptions() {
      return new SubscriptionOptions(Topic, QualityOfServiceLevel.ExactlyOnce);
    }
  }

}
