using DAT.Utils;

namespace DAT.Configuration {
  public interface IConfiguration : ICloneable {
    string Url { get; set; }

    string Type { get; set; }

    bool MonitorConfiguration { get; set; }

    int MonitorIntervalMilliseconds { get; set; }
    
    event EventHandler<EventArgs<IConfiguration>> ConfigurationChanged;

    void ChangeConfiguration(IConfiguration newConfiguration); // performs changes and fires ConfigurationChanged
  }
}