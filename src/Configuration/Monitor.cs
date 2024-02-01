using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;

namespace Ai.Hgb.Dat.Configuration {
  public class Monitor<T> : BackgroundService where T : IConfiguration {
    public T Configuration { get { return configuration; } set { configuration = value; } }
    public IParser Parser { get { return parser; } private set { parser = value; } }
    public int MonitorIntervalMilliseconds { get { return monitorIntervalMilliseconds; } set { if (monitorIntervalMilliseconds != value) monitorIntervalMilliseconds = value; } }

    private T configuration;
    private IParser parser;
    private int monitorIntervalMilliseconds;
    private CancellationTokenSource cts;

    public Monitor() { }

    public Monitor(IParser parser) {
      this.parser = parser;
    }

    public void Initialize(string uri) {
      parser = Ai.Hgb.Dat.Configuration.Parser.GetParser(uri);      
      configuration = parser.Parse<T>(uri);
      monitorIntervalMilliseconds = Configuration.MonitorIntervalMilliseconds;
      cts = new CancellationTokenSource();
    }

    protected override async Task ExecuteAsync(CancellationToken token) {
      if(configuration == null || monitorIntervalMilliseconds == 0 || parser == null) {
        throw new ArgumentNullException("Non-optional parameters are missing (Configuration, TimeOutMilliseconds, Parser)");
      }


      var file = new FileInfo(Configuration.Url);
      var lastUpdate = file.LastWriteTime;
      var stop = false;

      while(!token.IsCancellationRequested && !stop) {
        if(File.Exists(configuration.Url)) {
          file = new FileInfo(configuration.Url);
          if(file.LastWriteTime > lastUpdate) {
            var newConfiguration = parser.Parse(configuration.Url);
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
