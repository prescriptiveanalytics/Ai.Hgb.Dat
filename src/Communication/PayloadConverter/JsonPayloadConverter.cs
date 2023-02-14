using System.Text;
using System.Text.Json;

namespace DCT.Communication {
  public class JsonPayloadConverter : IPayloadConverter {
    public T Deserialize<T>(byte[] payload) {
      return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(payload));
    }

    public byte[] Serialize<T>(T payload) {
      return JsonSerializer.SerializeToUtf8Bytes<T>(payload);
    }
  }
}
