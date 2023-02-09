using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class ActiveMetadataModel : RunTimeMetaDataModel
    {
        public ActiveMetadataModel(RecordingHandler pageRecordingHandler)
        {
            Descriptions = _populateFromHandler(pageRecordingHandler, "");
        }

        public ActiveMetadataModel(RecordingHandler pageRecordingHandler, string recordingId)
        {
            RecordingId = recordingId;
            Descriptions = _populateFromHandler(pageRecordingHandler, recordingId);
        }

        public string RecordingId { get; set; }

        private List<ActionDescription> _populateFromHandler(RecordingHandler handler, string recordingId)
        {
            var sanitizers = (IEnumerable<RecordedTestSanitizer>) handler.Sanitizers;
            var transforms = (IEnumerable<ResponseTransform>) handler.Transforms;
            var matcher = handler.Matcher;

            //List<ConcurrentDictionary<string, ModifiableRecordSession>> searchCollections = new List<ConcurrentDictionary<string, ModifiableRecordSession>>()
            //{
            //    handler.PlaybackSessions,
            //    handler.RecordingSessions,
            //    handler.InMemorySessions
            //}; 
            // TODO: convert this guy

            if (!string.IsNullOrWhiteSpace(recordingId)){
                List<RecordedTestSanitizer> additionalSanitizers = new List<RecordedTestSanitizer>();

                if(handler.RecordingSessions.TryGetValue(recordingId, out var recordSession))
                {
                    sanitizers = sanitizers.Concat(recordSession.AdditionalSanitizers);
                    transforms = transforms.Concat(recordSession.AdditionalTransforms);

                    if (recordSession.CustomMatcher != null)
                    {
                        matcher = recordSession.CustomMatcher;
                    }
                }
                else if (handler.PlaybackSessions.TryGetValue(recordingId, out var playbackSession))
                {
                    sanitizers = sanitizers.Concat(playbackSession.AdditionalSanitizers);
                    transforms = transforms.Concat(playbackSession.AdditionalTransforms);

                    if (playbackSession.CustomMatcher != null)
                    {
                        matcher = playbackSession.CustomMatcher;
                    }
                }
            }

            List<ActionDescription> descriptions = new List<ActionDescription>();
            var docXML = GetDocCommentXML();

            descriptions.AddRange(sanitizers.Select(x => new ActionDescription()
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
                Name = matcher.GetType().Name,
                ConstructorDetails = GetInstanceDetails(matcher),
                Description = GetClassDocComment(matcher.GetType(), docXML)
            });

            return descriptions;
        }
    }
}
