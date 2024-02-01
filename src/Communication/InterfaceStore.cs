using System.Text;

namespace Ai.Hgb.Dat.Communication {

  public enum CommunicationMode {
    Publish,
    Subscribe,
    Request,
    Respond,
    Property
  }

  public class InterfaceStore {

    public static Dictionary<CommunicationMode, string> CommunicationModeStrings = new Dictionary<CommunicationMode, string> { 
      { CommunicationMode.Publish, "output" },
      { CommunicationMode.Subscribe, "input" },
      { CommunicationMode.Request, "request" },
      { CommunicationMode.Respond, "response" },
      { CommunicationMode.Property, "property" }
    };

    private Dictionary<Tuple<string, CommunicationMode>, Type> store;
    private Dictionary<string, Type> types;

    private string clientId;

    public InterfaceStore(string clientId) {
      this.clientId = clientId; 

      store = new Dictionary<Tuple<string, CommunicationMode>, Type>();
      types = new Dictionary<string, Type>();
    }

    public void Register<T>(string id, CommunicationMode mode) {
      var t = typeof(T);
      var key = Tuple.Create(id, mode);
      if (!store.ContainsKey(key)) store.Add(key, t);
      else store[key] = t;

      types.TryAdd(t.FullName, t);
    }

    /* sample Sidl text:
     * 
     * struct Document {
     *   string Id,
     *   string Author,
     *   string Text
     * }
     * 
     * message DocumentMessage { 
     *   Document document1
     * }
     * 
     * nodetype <clientId> {
     *   output DocumentMessage document
     * }
    */
    public string GenerateSidlText() {
      var sb = new StringBuilder();

      // types
      ExtractTypes(types.Values.ToList(), sb);

      sb.AppendLine();
      sb.AppendLine();

      // messages      
      foreach (var key in store.Keys.Where(k => k.Item2 != CommunicationMode.Property)) {
        sb.AppendLine($"message {store[key].Name}Message " + "{");
        sb.AppendLine($"\t{store[key].Name} {store[key].Name.ToLower()}");
        sb.AppendLine("}");
      }

      sb.AppendLine();
      sb.AppendLine();

      // nodetype
      sb.AppendLine($"nodetype {clientId} " + "{");
      foreach (var key in store.Keys.Where(k => k.Item2 == CommunicationMode.Property)) {
        sb.AppendLine($"\t{GetKeyword(CommunicationMode.Property)} {store[key].Name} {key.Item1}");
      }

      int count = 0;
      foreach (var key in store.Keys.Where(k => k.Item2 != CommunicationMode.Property)) {
        count++;
        sb.AppendLine($"\t{GetKeyword(key.Item2)} {store[key].Name}Message {store[key].Name.ToLower()}Message{count}");
      }
      sb.AppendLine("}");

      return sb.ToString();
    }

    public void ExtractTypes(List<Type> typeList, StringBuilder sb) {
      for(int i = 0; i < typeList.Count; i++) {
        var t = typeList[i];
        bool isStruct = t.IsValueType && !t.IsPrimitive;
        bool isClass = t.IsClass && t != typeof(string);     

        if (isStruct || isClass) {
          sb.AppendLine($"struct {t.Name} " + "{");
          var properties = t.GetProperties();
          foreach (var p in properties) {

            bool isStructOrClass = (p.PropertyType.IsValueType && !p.PropertyType.IsPrimitive) || (p.PropertyType.IsClass && p.PropertyType != typeof(string));
            if (isStructOrClass) typeList.Insert(i + 1, p.PropertyType);

            sb.AppendLine($"\t{GetKeyword(p.PropertyType)} {p.Name}");
          }
          sb.AppendLine("}");          
        }        
      }
    }


    public void ExtractTypeFullRecursive(Type t, string name, StringBuilder sb, int level) {
      bool isStruct = t.IsValueType && !t.IsPrimitive;
      bool isClass = t.IsClass;
      bool isPrimitiveValueType = !isStruct;

      if(isStruct || isClass) {
        AppendIndent(sb, level);
        sb.AppendLine($"struct {t.Name} " + "{");

        var properties = t.GetProperties();
        foreach(var p in properties) {
          ExtractTypeFullRecursive(p.GetType(), p.Name, sb, level + 1);           
        }

        AppendIndent(sb, level);
        sb.AppendLine("}");
      } else if (isPrimitiveValueType) {        
        AppendIndent(sb, level);
        sb.AppendLine($"{t.Name} {name}");        
      }
    }

    public void AppendIndent(StringBuilder sb, int level) {
      for (int i = 0; i < level; i++) sb.Append("\t");
    }

    public string GetKeyword(CommunicationMode mode) {
      return CommunicationModeStrings[mode];  
    }

    public string GetKeyword(Type t) {
      if (t == typeof(string)) return "string";
      return t.Name;
    }

    public string FirstCharToUpper(string input) {
      if (string.IsNullOrEmpty(input)) {
        return string.Empty;
      }
      return $"{char.ToUpper(input[0])}{input[1..]}";
    }

    public string FirstCharToLower(string input) {
      if (string.IsNullOrEmpty(input)) {
        return string.Empty;
      }
      return $"{char.ToLower(input[0])}{input[1..]}";
    }

  }
}
