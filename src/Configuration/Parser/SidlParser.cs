using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ai.Hgb.Dat.Configuration {
  public class SidlParser : IParser {
    public IConfiguration Parse(string uri) {
      throw new NotImplementedException();
    }

    public T Parse<T>(string uri) where T : IConfiguration {
      throw new NotImplementedException();
    }
  }
}
