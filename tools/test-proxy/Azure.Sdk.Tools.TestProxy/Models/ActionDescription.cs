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

    public class CtorDescription
    {
        public string Description;
        public List<Tuple<string, string>> Arguments;
    }

    public class ActionDescription
    {
        public MetaDataType ActionType;
        public string Name;
        public string Description;
        public CtorDescription ConstructorDetails;

        public ActionDescription() { }

        public ActionDescription(string nameSpace)
        {
            var nsValue = nameSpace.Split(".").Last();

            if (Enum.TryParse(nsValue.Substring(0, nsValue.Length - 1), out MetaDataType actionType))
            {
                ActionType = actionType;
            };
        }
    }
}
