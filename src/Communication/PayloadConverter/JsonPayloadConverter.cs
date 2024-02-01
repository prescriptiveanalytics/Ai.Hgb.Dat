using System.Text;
using System.Text.Json;

namespace Ai.Hgb.Dat.Communication {
  public class JsonPayloadConverter : IPayloadConverter {
    public T Deserialize<T>(byte[] payload) {
      if (payload == null) return default(T);
      return JsonSerializer.Deserialize<T>(payload);
      //return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(payload));
    }

    public object Deserialize(byte[] payload, Type type = null) {
      if (payload == null) return null;
      return JsonSerializer.Deserialize(Encoding.UTF8.GetString(payload), type != null ? type : typeof(object));
    }

    public byte[] Serialize<T>(T payload) {
      if (payload == null) return null;
      return JsonSerializer.SerializeToUtf8Bytes<T>(payload);
    }
  }
}
