using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class TransformTests
    {
        [Fact]
        public async Task ConditionalTransformAppliesForRegex()
        {
            var clientIdTransform = new StorageRequestIdTransform(condition: new ApplyCondition() { UriRegex = @".+/Tables.*" });
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.Transforms.Add(clientIdTransform);

            var playbackContext = new DefaultHttpContext();
            var transformedContent = "This should still show up.";
            var targetHeaderKey = "x-ms-client-request-id";
            var targetFile = "Test.RecordEntries/response_with_xml_body.json";
            var transformedEntry = TestHelpers.LoadRecordSession(targetFile).Session.Entries[0];
            transformedEntry.Request.Headers[targetHeaderKey] = new string[] { transformedContent };
            var untransformedEntry = TestHelpers.LoadRecordSession(targetFile).Session.Entries[1];
            var originalRequestHeader = untransformedEntry.Request.Headers[targetHeaderKey][0].ToString();
            untransformedEntry.Request.Headers[targetHeaderKey] = new string[] { "This shouldn't show up on response. The transform shouldn't apply." };

            // start playback
            playbackContext.Request.Headers["x-recording-file"] = targetFile;
            var controller = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await controller.Start();
            var recordingId = playbackContext.Response.Headers["x-recording-id"].ToString();

            // transform should apply only to first/last request
            HttpRequest transformedRequest = TestHelpers.CreateRequestFromEntry(transformedEntry);
            HttpResponse transformedResponse = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, transformedRequest, transformedResponse);
            Assert.Contains(transformedContent, transformedResponse.Headers["x-ms-client-request-id"].ToString());

            // this one should keep the x-ms-client-request-id that was in the recording, the transform should NOT apply
            HttpRequest nonTransformedRequest = TestHelpers.CreateRequestFromEntry(untransformedEntry);
            HttpResponse nonTransformedresponse = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, nonTransformedRequest, nonTransformedresponse);

            Assert.Equal(originalRequestHeader, nonTransformedresponse.Headers["x-ms-client-request-id"].ToString());
        }

        [Fact]
        public async Task CanApplyHeaderTransform()
        {
            var headerTransform = new HeaderTransform("Location", "http://localhost", valueRegex: @".*/Tables\(.*");
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.Transforms.Add(headerTransform);

            var playbackContext = new DefaultHttpContext();
            var targetFile = "Test.RecordEntries/response_with_header_to_transform.json";
            var transformedEntry = TestHelpers.LoadRecordSession(targetFile).Session.Entries[0];
            var untransformedEntry = TestHelpers.LoadRecordSession(targetFile).Session.Entries[1];

            // start playback
            playbackContext.Request.Headers["x-recording-file"] = targetFile;
            var controller = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await controller.Start();
            var recordingId = playbackContext.Response.Headers["x-recording-id"].ToString();

            // transform should apply only to first request
            HttpRequest request = TestHelpers.CreateRequestFromEntry(transformedEntry);
            HttpResponse response = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);
            Assert.Equal("http://localhost", response.Headers["Location"]);

            // this one should keep the original Location value
            request = TestHelpers.CreateRequestFromEntry(untransformedEntry);
            response = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);
            var originalLocation = untransformedEntry.Response.Headers["Location"];

            Assert.Equal(originalLocation, response.Headers["Location"]);
        }
    }
}
