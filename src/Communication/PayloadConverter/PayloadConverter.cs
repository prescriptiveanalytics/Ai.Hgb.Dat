using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.Communication {
  public class PayloadConverter {
    private static Dictionary<PayloadFormat, Type> payloadFormatDict
      = new Dictionary<PayloadFormat, Type>
    {
          { PayloadFormat.JSON, typeof(JsonPayloadConverter) }
          ,{ PayloadFormat.YAML, typeof(YamlPayloadConverter) }
          //,{ PayloadFormat.TOML, typeof(TomlPayloadConverter) }
          //,{ PayloadFormat.PROTOBUF, typeof(ProtobufPayloadConverter) }
          //,{ PayloadFormat.SIDL, typeof(SidlPayloadConverter) }
    };

    public static Type GetPayloadFormatType(PayloadFormat payloadFormat) {
      return payloadFormatDict[payloadFormat];
    }
  }
}
