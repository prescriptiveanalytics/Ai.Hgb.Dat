using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VYaml.Serialization;

namespace Ai.Hgb.Dat.Communication {
  public class VYamlPayloadConverter : IPayloadConverter {

    //private YamlSerializer ser;
    //private IDeserializer dser;

    public VYamlPayloadConverter() {
      //ser = new SerializerBuilder().IncludeNonPublicProperties().Build();
      //dser = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
    }

    public T Deserialize<T>(byte[] payload) {
      return YamlSerializer.Deserialize<T>(new ReadOnlyMemory<byte>(payload));
      //return dser.Deserialize<T>(Encoding.UTF8.GetString(payload));
    }

    public object Deserialize(byte[] payload, Type type = null) {
      return YamlSerializer.Deserialize<object>(new ReadOnlyMemory<byte>(payload));      
      //return dser.Deserialize(Encoding.UTF8.GetString(payload), type != null ? type : typeof(object));
    }

    public byte[] Serialize<T>(T payload) {
      return YamlSerializer.Serialize(payload).ToArray();
      //return Encoding.UTF8.GetBytes(ser.Serialize(payload));
    }
  }
}
