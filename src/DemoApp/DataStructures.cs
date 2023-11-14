using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAT.DemoApp {
  public struct Document {

    public string Id { get; set; }
    public string Author { get; set; }
    public string Text { get; set; }

    public Document(string id, string author, string text) {
      Id = id;
      Author = author;
      Text = text;
    }

    public override string ToString() {
      return $"Id: {Id}, author: {Author}";
    }
  }

  public class ComplexDocument {
    public string Id { get; set; }
    public Person Author { get; set; }
    public string Text { get; set; }

    public ComplexDocument(string id, Person author, string text) {
      Id = id;
      Author = author;
      Text = text;
    }

    public override string ToString() {
      return $"Id: {Id}, author: {Author}";
    }
  }

  public struct Person {
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Address Address { get; set; }

    public override string ToString() {
      return $"{FirstName} {LastName}, {Address}";
    }
  }

  public struct Address {
    public string City { get; set; }
    public int Zip { get; set; }

    public override string ToString() {
      return $"{Zip} {City}";
    }
  }

  public struct Point {
    public Point(byte tag, double x, double y) => (Tag, X, Y) = (tag, x, y);

    public byte Tag { get; }
    public double X { get; }
    public double Y { get; }
  }

  public class DoubleTypedPopulation {
    public double Fits { get; set; }
    public double[] Candidates { get; set; }
    public int Length { get; set; }

    public DoubleTypedPopulation() { }

    public DoubleTypedPopulation(int length) {
      this.Length = length;
      Candidates = new double[length];
      Fits = 0;
    }

    public DoubleTypedPopulation(double fits, double[] candidates) {
      Fits = fits;
      Candidates = candidates;
      Length = candidates.Length;
    }
  }

  public record DmonItem(string id, string group, int rank, string title, double value, string timestamp, string systemTimestamp);

}
