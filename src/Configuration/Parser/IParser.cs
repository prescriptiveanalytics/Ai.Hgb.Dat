using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ai.Hgb.Dat.Configuration {
  public interface IParser {
    IConfiguration Parse(string uri);

    T Parse<T>(string uri) where T : IConfiguration;
  }
}
