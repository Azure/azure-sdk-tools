using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class CommonMetadata
    {
        IEnumerable<ActionDescription> Descriptions;

        public CommonMetadata()
        {
            Descriptions = _populateFromMetadata();
        }

        public IEnumerable<ActionDescription> GetTransforms()
        {
            return Descriptions.Where(x => x.ActionType == MetaDataType.Transform);
        }

        public IEnumerable<ActionDescription> GetMatchers()
        {
            return Descriptions.Where(x => x.ActionType == MetaDataType.Matcher);
        }

        public IEnumerable<ActionDescription> GetSanitizers()
        {
            return Descriptions.Where(x => x.ActionType == MetaDataType.Sanitizer);
        }

        private List<ActionDescription> _populateFromMetadata()
        {


            return new List<ActionDescription>();
        }
    }
}
