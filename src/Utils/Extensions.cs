namespace Ai.Hgb.Dat.Utils {
  public static class Extensions {
    public static List<T> Clone<T>(this IEnumerable<T> listToClone) where T : ICloneable {
      return listToClone.Select(item => (T)item.Clone()).ToList();
    }
    public static double NextGaussian_BoxMuller(this Random rnd, double mean = 0.0, double stdDev = 1.0) {
      double u1 = rnd.NextDouble(); // uniform(0,1) random doubles
      double u2 = rnd.NextDouble();
      double rndStdNormal = Math.Cos(2.0 * Math.PI * u1) * Math.Sqrt(-2.0 * Math.Log(u2)); // random normal(0,1)
      return mean + stdDev * rndStdNormal;
    }
  }
}