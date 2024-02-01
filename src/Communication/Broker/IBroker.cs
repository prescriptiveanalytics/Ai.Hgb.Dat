using Ai.Hgb.Dat.Configuration;

namespace Ai.Hgb.Dat.Communication {
  public interface IBroker : IDisposable {
    HostAddress Address { get; }

    IBroker StartUp();
    
    Task StartUpAsync();

    void TearDown();

    Task TearDownAsync();
  }
}
