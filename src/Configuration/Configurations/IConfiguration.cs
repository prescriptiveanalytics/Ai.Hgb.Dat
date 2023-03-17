namespace DAT.Configuration {
  public interface IConfiguration {
    string URI { get; set; }
    bool MonitorConfig { get; set; }
    int MonitorIntervalMilliseconds { get; set; }

  }
}