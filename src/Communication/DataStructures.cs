using Ai.Hgb.Dat.Configuration;
using Ai.Hgb.Dat.Utils;
using MemoryPack;
using System.Net.Mime;
using System.Xml.Serialization;
using VYaml.Annotations;
using YamlDotNet.Serialization;

namespace Ai.Hgb.Dat.Communication {

  //[VYaml.Annotations.YamlObject]
  //[VYaml.Annotations.YamlObjectUnion("Imsg", typeof(Message))]
  public interface IMessage : ICloneable {
    string ClientId { get; set; }
    public string ClientName { get; set; }
    string Topic { get; set; }
    string ResponseTopic { get; set; }
    string ContentType { get; set; }
    byte[] Payload { get; set; }

    QualityOfServiceLevel QOS { get; set;}
    long Timestamp { get; set; }
    int ResponseCount { get; set; }
    bool BulkResponse { get; set; }


    object Content { get; set; }
  }

  //[VYaml.Annotations.YamlObject]
  public class Message : IMessage {

    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public string ContentType { get; set; }
    public byte[] Payload { get; set; }
    public string Topic { get; set; }
    public string ResponseTopic { get; set; }

    //[VYaml.Annotations.YamlIgnore]
    [YamlDotNet.Serialization.YamlIgnore]
    [XmlIgnore]
    public object Content { get; set; }

    public QualityOfServiceLevel QOS { get; set; }
    public long Timestamp { get; set; }
    public int ResponseCount { get; set; }
    public bool BulkResponse { get; set; }

    public Message() { }

    public Message(Message msg) {      
      ClientId = msg.ClientId;
      ClientName = msg.ClientName;
      Topic = msg.Topic;
      ResponseTopic = msg.ResponseTopic;
      ContentType = msg.ContentType;
      Payload = msg.Payload != null ? msg.Payload.ToArray() : msg.Payload;
      Timestamp= msg.Timestamp;
    }

    public Message(string clientId, string clientName, string topic, string responseTopic, string contentType, byte[] payload, QualityOfServiceLevel qos = QualityOfServiceLevel.ExactlyOnce) {
      ClientId = clientId;
      ClientName = clientName;
      Topic = topic;
      ResponseTopic = responseTopic;
      ContentType = contentType;
      Payload = payload != null ? payload.ToArray() : payload;
      QOS = qos;
      Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public Message(string clientId, string clientName, string topic, string responseTopic, string contentType, byte[] payload, QualityOfServiceLevel qos = QualityOfServiceLevel.ExactlyOnce, int responseCount = 1, bool bulkResponse = false) {
      ClientId = clientId;
      ClientName = clientName;
      Topic = topic;
      ResponseTopic = responseTopic;
      ContentType = contentType;
      Payload = payload != null ? payload.ToArray() : payload;
      QOS = qos;
      Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      ResponseCount = responseCount;
      BulkResponse = bulkResponse;
    }

    public object Clone() {
      return new Message(ClientId, ClientName, Topic, ResponseTopic, ContentType, Payload, QOS, ResponseCount, BulkResponse);
    }
  }

  //[VYaml.Annotations.YamlObject]
  [MemoryPackable]
  public partial class Message<T> : Message { // added partial
    
    public new T Content { get; set; }

    //[VYaml.Annotations.YamlConstructor]
    [MemoryPackConstructor]
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

    //public Message(string clientId, string clientName, string topic, string responseTopic, string contentType, byte[] payload, T content, QualityOfServiceLevel qos = QualityOfServiceLevel.ExactlyOnce, int responseCount = 1, bool bulkResponse = false)
    //  : base(clientId, clientName, topic, responseTopic, contentType, payload, qos) {
    //  Content = content;
    //}

    public new object Clone() {
      return new Message<T>(ClientId, ClientName, Topic, ResponseTopic, ContentType, Payload, Content, QOS);      
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

  [MemoryPackable]
  public partial class DoubleTypedArray {
    public double[] Value { get; set; }

    [MemoryPackConstructor]
    public DoubleTypedArray() { }

    public DoubleTypedArray(int length) {
      Value = new double[length];
    }
  }
}
