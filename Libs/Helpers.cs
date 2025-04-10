using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace VRSLAM.Libs
{
    public class Helpers
    {
        public static bool HasProperty(dynamic obj, string propertyName)
        {
            if (obj is ExpandoObject expando)
            {
                return ((IDictionary<string, object>)expando).ContainsKey(propertyName);
            }
            return false;
        }
    }
}