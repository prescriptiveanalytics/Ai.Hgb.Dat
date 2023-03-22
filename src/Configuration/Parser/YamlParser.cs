using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DAT.Configuration {
  public class YamlParser : IParser {

    private ISerializer ser;
    private IDeserializer dser;

    public YamlParser() {
      ser = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreFields()                
        .Build();
      
      dser = new DeserializerBuilder()
        //.WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreFields()        
        .IgnoreUnmatchedProperties()
        .Build();
                  
    }

    public IConfiguration Parse(string uri) {
      string doc = Parser.ReadText(uri);
      Console.WriteLine(doc);
      IConfiguration config = dser.Deserialize<Configuration>(doc);
      
      if(config.Type == "Socket") {
        config = dser.Deserialize<SocketConfiguration>(doc);        
      }

      config.Uri = uri;
      return config;
    }

    public T Parse<T>(string uri) where T : IConfiguration {
      string doc = Parser.ReadText(uri);
      var config = dser.Deserialize<T>(doc);
      config.Uri = uri;

      return config;
    }
  }
}
