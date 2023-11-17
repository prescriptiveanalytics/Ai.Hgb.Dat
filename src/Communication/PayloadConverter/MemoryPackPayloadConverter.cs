using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAT.Communication {
  public class MemoryPackPayloadConverter : IPayloadConverter {
    public T Deserialize<T>(byte[] payload) {
      return MemoryPackSerializer.Deserialize<T>(payload);
    }

    public object Deserialize(byte[] payload, Type type = null) {
      return MemoryPackSerializer.Deserialize(type, payload);
    }

    public byte[] Serialize<T>(T payload) {
      return MemoryPackSerializer.Serialize(payload);
    }
  }
}
