
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Ai.Hgb.Dat.Communication {
  public class XmlPayloadConverter : IPayloadConverter {
    public T Deserialize<T>(byte[] payload) {
      if (payload == null) return default(T);
      var serializer = new XmlSerializer(typeof(T));
      return (T)serializer.Deserialize(new StringReader(Encoding.UTF8.GetString(payload)));
    }

    public object Deserialize(byte[] payload, Type type = null) {
      if (payload == null) return null;
      if (type == null) type = typeof(object);
      var serializer = new XmlSerializer(type);
      return serializer.Deserialize(new StringReader(Encoding.UTF8.GetString(payload)));
    }

    public byte[] Serialize<T>(T payload) {
      if (payload == null) return null;
      var serializer = new XmlSerializer(typeof(T));
      StringBuilder sb = new StringBuilder();
      XmlWriter writer = XmlWriter.Create(sb);
      serializer.Serialize(writer, payload);
      return Encoding.UTF8.GetBytes(sb.ToString());      
    }
  }
}
