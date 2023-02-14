using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DCT.Communication {
  public class YamlPayloadConverter : IPayloadConverter {

    private ISerializer ser;
    private IDeserializer dser;

    public YamlPayloadConverter() {
      ser = new SerializerBuilder().Build();
      dser = new DeserializerBuilder().Build();
    }

    public T Deserialize<T>(byte[] payload) {
      return dser.Deserialize<T>(Encoding.UTF8.GetString(payload));
    }

    public byte[] Serialize<T>(T payload) {
      return Encoding.UTF8.GetBytes(ser.Serialize(payload));
    }
  }
}
