using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class ActiveMetadataModel : RunTimeMetaDataModel
    {
        public ActiveMetadataModel(RecordingHandler pageRecordingHandler)
        {
            Descriptions = _populateFromHandler(pageRecordingHandler);
        }

        private List<ActionDescription> _populateFromHandler(RecordingHandler handler)
        {
            List<ActionDescription> descriptions = new List<ActionDescription>();
            var docXML = GetDocCommentXML();

            descriptions.AddRange(handler.Sanitizers.Select(x => new ActionDescription()
            {
                ActionType = MetaDataType.Sanitizer,
                Name = x.GetType().Name,
                ConstructorDetails = GetInstanceDetails(x),
                Description = GetClassDocComment(x.GetType(), docXML)
            }));

            descriptions.AddRange(handler.Transforms.Select(x => new ActionDescription()
            {
                ActionType = MetaDataType.Transform,
                Name = x.GetType().Name,
                ConstructorDetails = GetInstanceDetails(x),
                Description = GetClassDocComment(x.GetType(), docXML)
            }));

            descriptions.Add(new ActionDescription()
            {
                ActionType = MetaDataType.Matcher,
                Name = handler.Matcher.GetType().Name,
                ConstructorDetails = GetInstanceDetails(handler.Matcher),
                Description = GetClassDocComment(handler.Matcher.GetType(), docXML)
            });

            return descriptions;
        }
    }
}
