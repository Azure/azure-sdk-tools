using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public enum MetaDataType
    {
        Transform,
        Sanitizer,
        Matcher
    }

    public class ActionDescription
    {
        public MetaDataType ActionType;
        public string Name;
        public string Description;
        public string ConstructorDescription;
        public List<Tuple<string, string>> Arguments;
    }
}
