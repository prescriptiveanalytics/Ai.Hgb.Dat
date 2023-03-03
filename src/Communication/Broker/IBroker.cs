namespace DAT.Communication {
  public interface IBroker {
    HostAddress Address { get; }

    void StartUp();
    
    Task StartUpAsync();

    void TearDown();

    Task TearDownAsync();
  }
}
