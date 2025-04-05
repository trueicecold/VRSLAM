using System.Text.Json;
using System.Dynamic;

namespace VRSLAM.Libs
{
    public class JSON
    {
        public static string Stringify(object obj)
        {
            return JsonSerializer.Serialize(obj);
        }
        
        public static ExpandoObject Parse(string json)
        {
            return JsonSerializer.Deserialize<ExpandoObject>(json);
        }
    }
}