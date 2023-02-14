namespace DCT.Communication {
  public interface IPayloadConverter {
    byte[] Serialize<T>(T payload);

    T Deserialize<T>(byte[] payload);

  }
}
