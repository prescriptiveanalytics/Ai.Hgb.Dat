using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAT.Configuration {
  public static class Parser {

    private static YamlParser yamlParser = null;
    private static SidlParser sidlParser = null;

    public static IParser GetParser(string uri) {
      if (String.IsNullOrEmpty(uri)) throw new ArgumentNullException("The provided argument does not point to a valid configuration file.");

      IParser parser = null;

      if (File.Exists(uri)) {
        string ext = Path.GetExtension(uri);
        if (ext == ".yaml" || ext == ".yml") {
          if (yamlParser == null) yamlParser = new YamlParser();
          parser = yamlParser;
        }
        else if (ext == ".3l") {
          if (sidlParser == null) sidlParser = new SidlParser();
          parser = sidlParser;
        }
      }
      else {
        // handle http(s) URI
      }

      return parser;
    }

    public static IConfiguration Parse(string uri) {
      return GetParser(uri).Parse(uri);
    }

    public static T Parse<T>(string uri) where T : IConfiguration {
      return GetParser(uri).Parse<T>(uri);
    }


    private static Dictionary<string, Type> ExtensionDict = new Dictionary<string, Type> {
      { ".yaml", typeof(YamlParser) }
      ,{ ".yml", typeof(YamlParser) }
      ,{ ".3l", typeof(SidlParser) }
    };

    public static string ReadText(string uri) {
      if (String.IsNullOrEmpty(uri)) throw new ArgumentNullException("The provided argument does not point to a valid configuration file.");

      return File.ReadAllText(uri);
    }

    private static string CheckConfigFilePath(string uri, out Type parserType) {
      string configFilePath = null;
      parserType = null;

      if (uri.Length > 0
        && !String.IsNullOrWhiteSpace(uri)
        && File.Exists(uri)) {

        string ext = Path.GetExtension(uri);
        if (ExtensionDict.ContainsKey(ext)) {
          configFilePath = uri;
          parserType = ExtensionDict[ext];
        }
        else {
          throw new ArgumentException("The provided argument does not point to a valid configuration file.");
        }
      }
      else {
        foreach (var ext in ExtensionDict) {
          var tmpFilePath = $"./config{ext.Key}";
          if (File.Exists(tmpFilePath)) {
            configFilePath = tmpFilePath;
            parserType = ext.Value;
            break;
          }
        }
        throw new ArgumentException("No valid configuration file could be found.");
      }

      return configFilePath;
    }



  }
}
