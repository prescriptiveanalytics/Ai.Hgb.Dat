using DAT.Utils;

namespace DAT.Configuration {
  public interface IConfiguration : ICloneable {    

    string Type { get; set; }
    public string Url { get; set; }
    bool MonitorConfiguration { get; set; }

    int MonitorIntervalMilliseconds { get; set; }
    
    event EventHandler<EventArgs<IConfiguration>> ConfigurationChanged;

    void ChangeConfiguration(IConfiguration newConfiguration); // performs changes and fires ConfigurationChanged
  }
}