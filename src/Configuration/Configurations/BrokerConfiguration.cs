using DAT.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DAT.Configuration {
  public class BrokerConfiguration : IConfiguration {
    public string Type { get; set; }
    public string Url { get; set; }
    public bool MonitorConfiguration { get; set; }
    public int MonitorIntervalMilliseconds { get; set; }

    public event EventHandler<EventArgs<IConfiguration>> ConfigurationChanged;

    public string Name { get; set; }
    public string Id { get; set; }
    public HostAddress Address { get; set; }
    public string BaseTopic { get; set; }
    public string PayloadType { get; set; }

    public BrokerConfiguration() {

    }

    public BrokerConfiguration(string type, string url, bool monitorConfiguration, int monitorIntervalMilliseconds, string name, string id, HostAddress address, string baseTopic, string payloadType) {
      Type = type;
      Url = url;
      MonitorConfiguration = monitorConfiguration;
      MonitorIntervalMilliseconds = monitorIntervalMilliseconds;
      Name = name;
      Id = id;
      Address = address;
      BaseTopic = baseTopic;
      PayloadType = payloadType;  
    }

    public object Clone() {
      var c = new BrokerConfiguration();

      c.Type = Type;
      c.Url = Url;
      c.MonitorConfiguration = MonitorConfiguration;
      c.MonitorIntervalMilliseconds = MonitorIntervalMilliseconds;
      c.Name = Name;
      c.Id = Id;
      c.Address = Address;
      c.BaseTopic = BaseTopic;
      c.PayloadType = PayloadType;

      return c;
    }

    public void ChangeConfiguration(IConfiguration newConfiguration) {
      if (!(newConfiguration is SocketConfiguration)) throw new ArgumentException("The given argument is not of the type SocketConfiguration.");
      var c = newConfiguration as SocketConfiguration;

      // perform all changes
      Name = c.Name;
      Id = c.Id;
      BaseTopic = c.BaseTopic;

      var handler = ConfigurationChanged;
      if (handler != null) handler(this, new EventArgs<IConfiguration>(this));
    }
  }
}
