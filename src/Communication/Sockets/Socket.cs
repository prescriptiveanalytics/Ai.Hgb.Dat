using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.Communication {
  public class Socket {
    private static Dictionary<SocketType, Type> socketTypeDict
  = new Dictionary<SocketType, Type>
{
        { SocketType.MQTT, typeof(MqttSocket) }
        ,{ SocketType.APACHEKAFKA, typeof(ApachekafkaSocket) }
};

    public static Type GetSocketType(SocketType socketType) {
      return socketTypeDict[socketType];
    }
  }
}
