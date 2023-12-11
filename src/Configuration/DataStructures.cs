using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DAT.Configuration {
  public class HostAddress {
    public HostAddress() { }

    public HostAddress(string name, int port) {
      Name = name;
      Port = port;
    }

    [YamlMember(Alias = "HostName")]
    public string Name { get; set; }
    public int Port { get; set; }

    public string Address {
      get => $"{Name}:{Port}";
    }

    public override string ToString() {
      return $"{Name}:{Port}";
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

  public class SubscriptionOptions : ICloneable {

    public string Topic { get; set; }
    public QualityOfServiceLevel QosLevel { get; set; }
    public Type ContentType { get; set; }

    public string ContentTypeFullName { get; set; } 

    public SubscriptionOptions() {
      Topic = null;
      QosLevel = QualityOfServiceLevel.ExactlyOnce;
      ContentType = null;      
    }

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

    public string Topic { get; set; }
    public string ResponseTopic { get; set; }
    public QualityOfServiceLevel QosLevel { get; set; }

    // TODO:
    // mqtt broker: 1. maintain queue per topic; 2. setup own socket;
    // 3. intercept subscriptions to queue topic add client-individual postfix, store subscriptions
    // 4. intercept all queue-direct messages and do not dispatch them; instead resend them to client-individual-subscriptions
    // 5. delete acknowledged messages
    // mqtt client: check if topic uses work queue and send additional ack message after subscribed handler task(s) are completed
    public bool UseWorkQueue;

    public PublicationOptions() {
      Topic = null;
      ResponseTopic = null;
      QosLevel = QualityOfServiceLevel.ExactlyOnce;
    }


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

    public string Topic { get; set; }
    public string ResponseTopic { get; set; }
    public bool GenerateResponseTopicPostfix { get; set; }
    public Type ContentType { get; set; }

    public string ContentTypeFullName { get; set; }

    public RequestOptions() {
      Topic = null;
      ResponseTopic = null;
      GenerateResponseTopicPostfix = true;
      ContentType = null;
      ContentTypeFullName = "";
    }

    public RequestOptions(string topic, string responseTopic, bool generateResponseTopicPostfix = true, Type contentType = null, string contentTypeFullName = "") {
      Topic = topic;
      ResponseTopic = responseTopic;
      GenerateResponseTopicPostfix = generateResponseTopicPostfix;
      ContentType = contentType;
      ContentTypeFullName = contentTypeFullName;
    }

    public object Clone() {
      return new RequestOptions(Topic, ResponseTopic, GenerateResponseTopicPostfix, ContentType, ContentTypeFullName);
    }

    public SubscriptionOptions GetRequestSubscriptionOptions() {
      return new SubscriptionOptions(Topic, QualityOfServiceLevel.ExactlyOnce, ContentType);
    }

    public SubscriptionOptions GetResponseSubscriptionOptions() {
      return new SubscriptionOptions(ResponseTopic, QualityOfServiceLevel.ExactlyOnce);
    }
  }

}
