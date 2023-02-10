namespace DCT.Communication {
  public interface IBroker {
    HostAddress Address { get; set; }

    void StartUp();

    void TearDown();
  }
}
