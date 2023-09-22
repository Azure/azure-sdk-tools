using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.IO;
using Azure.Sdk.Tools.TestProxy.Common;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Operations;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class ActiveMetadataModel : RunTimeMetaDataModel
    {
        public ActiveMetadataModel(string recordingId)
        {
            RecordingId = recordingId;
        }

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

            List<ConcurrentDictionary<string, ModifiableRecordSession>> searchCollections = new List<ConcurrentDictionary<string, ModifiableRecordSession>>()
            {
                handler.PlaybackSessions,
                handler.RecordingSessions,
                handler.InMemorySessions
            };

            var recordingFound = false;
            if (!string.IsNullOrWhiteSpace(recordingId)){
                foreach (var sessionDict in searchCollections)
                { 
                    if (sessionDict.TryGetValue(recordingId, out var session))
                    {
                        sanitizers = sanitizers.Concat(session.AdditionalSanitizers);
                        transforms = transforms.Concat(session.AdditionalTransforms);

                        if (session.CustomMatcher != null)
                        {
                            matcher = session.CustomMatcher;
                        }

                        recordingFound = true;
                        break;
                    }
                }

                if (!recordingFound)
                {
                    throw new SessionNotActiveException($"{recordingId} is not found in any Playback, Recording, or In-Memory sessions.");
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
