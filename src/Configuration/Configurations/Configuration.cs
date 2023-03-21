using DAT.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAT.Configuration {
  public class Configuration : IConfiguration {
    public string Type { get; set; }
    public string Uri { get; set; }
    public bool MonitorConfiguration { get; set; }
    public int MonitorIntervalMilliseconds { get; set; }

    public event EventHandler<EventArgs<IConfiguration>> ConfigurationChanged;

    public void ChangeConfiguration(IConfiguration newConfiguration) {
      throw new NotImplementedException();
    }
  }
}
