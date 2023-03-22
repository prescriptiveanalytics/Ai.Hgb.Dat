using DAT.Configuration;
using System.Net.Mime;

namespace DAT.Communication {

  public interface IMessage : ICloneable {
    string ClientId { get; set; }
    public string ClientName { get; set; }
    string Topic { get; set; }
    string ResponseTopic { get; set; }
    string ContentType { get; set; }
    byte[] Payload { get; set; }

    QualityOfServiceLevel QOS { get; set;}

    object Content { get; set; }
  }

  public class Message : IMessage {

    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public string ContentType { get; set; }
    public byte[] Payload { get; set; }
    public string Topic { get; set; }
    public string ResponseTopic { get; set; }
    public object Content { get; set; }
    public QualityOfServiceLevel QOS { get; set; }


    public Message() { }

    public Message(Message msg) {      
      ClientId = msg.ClientId;
      ClientName = msg.ClientName;
      Topic = msg.Topic;
      ResponseTopic = msg.ResponseTopic;
      ContentType = msg.ContentType;
      Payload = msg.Payload != null ? msg.Payload.ToArray() : msg.Payload;
    }

    public Message(string clientId, string clientName, string topic, string responseTopic, string contentType, byte[] payload, QualityOfServiceLevel qos = QualityOfServiceLevel.ExactlyOnce) {
      ClientId = clientId;
      ClientName = clientName;
      Topic = topic;
      ResponseTopic = responseTopic;
      ContentType = contentType;
      Payload = payload != null ? payload.ToArray() : payload;
      QOS = qos;
    }

    public object Clone() {
      return new Message(ClientId, ClientName, Topic, ResponseTopic, ContentType, Payload, QOS);
    }
  }

  public class Message<T> : Message {

    public new T Content { get; set; }

    public Message() { }

    public Message(Message msg) : base(msg) { }

    public Message(string clientId, string clientName, string topic, string responseTopic, string contentType, byte[] payload, T content, QualityOfServiceLevel qos = QualityOfServiceLevel.ExactlyOnce) 
      : base(clientId, clientName, topic, responseTopic, contentType, payload, qos) {
      Content = content;
    }
    public Message(string clientId, string clientName, string topic, string responseTopic, string contentType, T content, QualityOfServiceLevel qos = QualityOfServiceLevel.ExactlyOnce)
      : base(clientId, clientName, topic, responseTopic, contentType, null, qos) {
      Content = content;
    }

    public new object Clone() {
      return new Message<T>(ClientId, ClientName, Topic, ResponseTopic, ContentType, Payload, Content);
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
}
