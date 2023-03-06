﻿using System.Net.Mime;

namespace DAT.Communication {
  public struct HostAddress {
    public HostAddress(string server, int port) {
      Server = server;
      Port = port;
    }

    public string Server;
    public int Port;
    public string Address {
      get => $"{Server}:{Port}";
    }

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

    // TODO:
    // mqtt broker: 1. maintain queue per topic; 2. setup own socket;
    // 3. intercept subscriptions to queue topic add client-individual postfix, store subscriptions
    // 4. intercept all queue-direct messages and do not dispatch them; instead resend them to client-individual-subscriptions
    // 5. delete acknowledged messages
    // mqtt client: check if topic uses work queue and send additional ack message after subscribed handler task(s) are completed
    public bool UseWorkQueue; 


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

    public SubscriptionOptions GetRequestSubscriptionOptions() {
      return new SubscriptionOptions(Topic, QualityOfServiceLevel.ExactlyOnce);
    }

    public SubscriptionOptions GetResponseSubscriptionOptions() {
      return new SubscriptionOptions(ResponseTopic, QualityOfServiceLevel.ExactlyOnce);
    }
  }

}
