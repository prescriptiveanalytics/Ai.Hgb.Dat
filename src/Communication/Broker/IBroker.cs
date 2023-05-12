using DAT.Configuration;

namespace DAT.Communication {
  public interface IBroker : IDisposable {
    HostAddress Address { get; }

    IBroker StartUp();
    
    Task StartUpAsync();

    void TearDown();

    Task TearDownAsync();
  }
}
