using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;

namespace DAT.Configuration {
  public class Monitor : BackgroundService {
    public IConfiguration Configuration { get { return configuration; } set { if (configuration != value) configuration = value; } }
    public IParser Parser { get { return parser; } private set { parser = value; } }
    public int MonitorIntervalMilliseconds { get { return monitorIntervalMilliseconds; } set { if (monitorIntervalMilliseconds != value) monitorIntervalMilliseconds = value; } }

    private IConfiguration configuration;
    private IParser parser;
    private int monitorIntervalMilliseconds;
    private CancellationTokenSource cts;

    public Monitor(IParser parser) {
      this.parser = parser;
    }

    public void Initialize<T>(string uri) where T : IConfiguration {
      configuration = Parser.Parse<T>(uri);
      monitorIntervalMilliseconds = Configuration.MonitorIntervalMilliseconds;
      cts = new CancellationTokenSource();
    }

    protected override async Task ExecuteAsync(CancellationToken token) {
      if(configuration == null || monitorIntervalMilliseconds == 0 || parser == null) {
        throw new ArgumentNullException("Non-optional parameters are missing (Configuration, TimeOutMilliseconds, Parser)");
      }


      var file = new FileInfo(Configuration.Uri);
      var lastUpdate = file.LastWriteTime;
      var stop = false;

      while(!token.IsCancellationRequested && !stop) {
        if(File.Exists(configuration.Uri)) {
          file = new FileInfo(configuration.Uri);
          if(file.LastWriteTime > lastUpdate) {
            var newConfiguration = parser.Parse(configuration.Uri);
            configuration.ChangeConfiguration(newConfiguration);
          }
        } else {
          stop = true;
        }
        await Task.Delay(monitorIntervalMilliseconds);
      }
    }

    public void Start() {
      StartAsync(cts.Token);
    }

    public void Stop() {
      cts.Cancel();
    }    
  }
}
