using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class CommonMetadataModel : PageModel
    {
        IEnumerable<ActionDescription> Descriptions;

        public int Length { get { return Descriptions.Count(); } }

        public CommonMetadataModel()
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
            return new List<ActionDescription>() { new ActionDescription(){
                ActionType = MetaDataType.Matcher,
                Arguments = null,
                Description = "This is a test!",
                StringDescription = "this is so a test!",
                Name = "TestMatcher!"
            }};
        }
    }
}
