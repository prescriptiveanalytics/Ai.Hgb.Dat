using System.Net.Mime;

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

  public interface IMessage : ICloneable {
    string ClientId { get; set; }
    string ContentType { get; set; }
    byte[] Payload { get; set; }
    string Topic { get; set; }
    string ResponseTopic { get; set; }
  }

  public class Message : IMessage {

    public string ClientId { get; set; }
    public string ContentType { get; set; }
    public byte[] Payload { get; set; }
    public string Topic { get; set; }
    public string ResponseTopic { get; set; }

    public Message() { }

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

  public class Message<T> : Message {

    public T Content { get; set; }

    public Message() {}

    public Message(string clientId, string contentType, byte[] payload, string topic, string responseTopic, T content) 
      : base(clientId, contentType, payload, topic, responseTopic) {
      Content = content;
    }

    public new object Clone() {
      return new Message<T>(ClientId, ContentType, Payload, Topic, ResponseTopic, Content);
    }
  }

  public class EventArgs<T> : EventArgs {
    public T Value { get; private set; }

    public EventArgs(T value) {
      Value = value;
    }
  }

  public class ActionItem : ICloneable {

    public Action<IMessage, CancellationToken> Action;
    public CancellationToken Token;

    public ActionItem(Action<IMessage, CancellationToken> action, CancellationToken token) {
      Action = action;
      Token = token;
    }

    public object Clone() {
      return new ActionItem(Action, Token);
    }
  }

  public class ActionItem<T> : ICloneable {

    public Action<T, CancellationToken> Action;
    public CancellationToken Token;

    public ActionItem(Action<T, CancellationToken> action, CancellationToken token) {
      Action = action;
      Token = token;
    }

    public object Clone() {
      return new ActionItem<T>(Action, Token); 
    }
  }

  public class SubscriptionOptions : ICloneable {

    public string Topic;
    public QualityOfServiceLevel QosLevel;
    public Type ContentType;

    public SubscriptionOptions(string topic, QualityOfServiceLevel qosLevel, Type contentType = null) {
      Topic = topic;
      QosLevel = qosLevel;
      ContentType = contentType;
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

    public RequestOptions(string topic, string responseTopic, bool generateResponseTopicPostfix = true) {
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
