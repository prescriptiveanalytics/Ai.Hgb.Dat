﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Ai.Hgb.Dat.Communication {
  public class YamlPayloadConverter : IPayloadConverter {

    private ISerializer ser;
    private IDeserializer dser;

    public YamlPayloadConverter() {      
      ser = new SerializerBuilder().IncludeNonPublicProperties().Build();
      dser = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
    }

    public T Deserialize<T>(byte[] payload) {
      return dser.Deserialize<T>(Encoding.UTF8.GetString(payload));
    }

    public object Deserialize(byte[] payload, Type type = null) {
      return dser.Deserialize(Encoding.UTF8.GetString(payload), type != null ? type : typeof(object));      
    }

    public byte[] Serialize<T>(T payload) {
      return Encoding.UTF8.GetBytes(ser.Serialize(payload));
    }
  }
}
