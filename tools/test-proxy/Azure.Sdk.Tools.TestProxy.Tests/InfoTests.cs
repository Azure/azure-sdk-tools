using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class InfoTests
    {
        [Fact]
        public void TestReflectionModelBuild()
        {
            var testModel = new AvailableMetadataModel();

            // in general, check that these are populating correctly
            Assert.True(testModel.Sanitizers.Count() > 0);
            Assert.True(testModel.Matchers.Count() > 0);
            Assert.True(testModel.Transforms.Count() > 0);
 
            // double check to ensure that an class constructor descriptions and class description are populated properly from xml. 
            var documentedSanitizer = testModel.Sanitizers.First();
            var sampleArgTuple = documentedSanitizer.ConstructorDetails.Arguments.First();
            Assert.True(!String.IsNullOrEmpty(documentedSanitizer.Description));
            Assert.True(!String.IsNullOrEmpty(sampleArgTuple.Item2));
            Assert.True(documentedSanitizer.ActionType == MetaDataType.Sanitizer);

            var documentedMatcher = testModel.Matchers.First();
            Assert.True(documentedMatcher.ActionType == MetaDataType.Matcher);

            var documentedTransform = testModel.Transforms.First();
            Assert.True(documentedTransform.ActionType == MetaDataType.Transform);
        }

        [Fact]
        public void TestReflectionModelWithAdvancedType()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Sanitizers.Add(new GeneralRegexSanitizer(value: "A new value", condition: new ApplyCondition() { UriRegex= ".+/Tables" }));
            
            var controller = new Info(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            var result = controller.Active();
        }

        [Fact]
        public async Task TestReflectionModelWithTargetRecordSession()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            await testRecordingHandler.StartPlaybackAsync("Test.RecordEntries/multipart_request.json", httpContext.Response);
            testRecordingHandler.Transforms.Clear();

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            testRecordingHandler.AddSanitizerToRecording(recordingId, new UriRegexSanitizer(regex: "ABC123"));
            testRecordingHandler.AddSanitizerToRecording(recordingId, new BodyRegexSanitizer(regex: ".+?"));
            testRecordingHandler.SetMatcherForRecording(recordingId, new CustomDefaultMatcher(compareBodies: false, excludedHeaders: "an-excluded-header"));

            var model = new ActiveMetadataModel(testRecordingHandler, recordingId);
            var descriptions = model.Descriptions.ToList();

            // we should have exactly 6 if we're counting all the customizations appropriately
            Assert.True(descriptions.Count == 6);
            Assert.True(model.Matchers.Count() == 1);
            Assert.True(model.Sanitizers.Count() == 5);

            // confirm that the overridden matcher is showing up
            Assert.True(descriptions[3].ConstructorDetails.Arguments[1].Item2 == "\"ABC123\"");
            Assert.True(descriptions[4].ConstructorDetails.Arguments[1].Item2 == "\".+?\"");

            // and finally confirm our sanitizers are what we expect
            Assert.True(descriptions[5].Name == "CustomDefaultMatcher");
        }
    }
}
