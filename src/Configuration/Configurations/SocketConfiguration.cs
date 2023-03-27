using DAT.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAT.Configuration {
  public class SocketConfiguration : IConfiguration {
    public string Type { get; set; }
    public string Url { get; set; }
    public bool MonitorConfiguration { get; set; }
    public int MonitorIntervalMilliseconds { get; set; }

    public event EventHandler<EventArgs<IConfiguration>> ConfigurationChanged;


    // socket specifics
    public string Name { get; set; }
    public string Id { get; set; }

    public string SocketType { get; set; }

    public HostAddress Broker { get; set; }

    public string BaseTopic { get; set; }

    public string PayloadType { get; set; }

    public PublicationOptions DefaultPublicationOptions { get; set; }

    public SubscriptionOptions DefaultSubscriptionOptions { get; set; }

    public RequestOptions DefaultRequestOptions { get; set; }
    

    public SocketConfiguration() { }

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
