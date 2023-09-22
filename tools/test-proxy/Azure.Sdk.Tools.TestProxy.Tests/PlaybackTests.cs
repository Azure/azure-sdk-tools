using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Xunit;
using System.Text.Json;
using Azure.Sdk.Tools.TestProxy.Store;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class PlaybackTests
    {

        private NullLoggerFactory _nullLogger = new NullLoggerFactory();

        [Fact]
        public async void TestStartPlaybackSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var path = "Test.RecordEntries/requests_with_continuation.json";
            var body = $"{{\"x-recording-file\":\"{path}\"}}";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();

            var value = httpContext.Response.Headers["x-recording-id"].ToString();
            var recordLocation = httpContext.Response.Headers["x-base64-recording-file-location"].ToString();
            Assert.False(String.IsNullOrEmpty(value));
            Assert.False(String.IsNullOrEmpty(recordLocation));
            var decodedRecordLocation = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(recordLocation));
            Assert.EndsWith(path, decodedRecordLocation, StringComparison.OrdinalIgnoreCase);
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(value));
        }

        [Fact]
        public async void TestStartPlaybackInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // get a recordingId that can be used for in-mem
            var recordContext = new DefaultHttpContext();
            var recordController = new Record(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = recordContext
                }
            };
            await recordController.Start();
            var inMemId = recordContext.Response.Headers["x-recording-id"].ToString();
            recordContext.Request.Headers["x-recording-id"] = new string[] { inMemId };
            recordController.Stop();

            // apply same recordingId when starting in-memory session
            var playbackContext = new DefaultHttpContext();
            playbackContext.Request.Headers["x-recording-id"] = inMemId;

            var playbackController = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await playbackController.Start();

            var value = playbackContext.Response.Headers["x-recording-id"].ToString();
            var recordLocation = playbackContext.Response.Headers["x-base64-recording-file-location"].ToString();
            Assert.False(String.IsNullOrEmpty(value));
            Assert.True(String.IsNullOrEmpty(recordLocation));
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(value));
            Assert.True(testRecordingHandler.InMemorySessions.Count() == 1);
        }

        [Fact]
        public async void TestStartPlaybackInMemoryThrowsInInvalidRecordingId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            var playbackContext = new DefaultHttpContext();
            var recordingId = Guid.NewGuid().ToString();
            playbackContext.Request.Headers["x-recording-id"] = recordingId;

            var playbackController = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };

            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await playbackController.Start()
            );

            Assert.Equal($"There is no in-memory session with id {recordingId} available for playback retrieval.", assertion.Message);
        }

        [Fact]
        public async void TestStartPlaybackThrowsOnInvalidInput()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            var playbackContext = new DefaultHttpContext();
            var recordingId = Guid.NewGuid().ToString();
            playbackContext.Request.Headers["x-recording-id"] = recordingId;
            var playbackController = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };

            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await playbackController.Start()
            );

            Assert.Equal($"There is no in-memory session with id {recordingId} available for playback retrieval.", assertion.Message);
        }

        [Fact]
        public async void TestStopPlaybackSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"Test.RecordEntries/requests_with_continuation.json\"}";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();
            var targetRecordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            
            httpContext.Request.Headers["x-recording-id"] = new string[] { targetRecordingId };
            controller.Stop();

            Assert.False(testRecordingHandler.PlaybackSessions.ContainsKey(targetRecordingId));
        }

        [Fact]
        public async void TestStopPlaybackInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // get a recordingId that can be used for in-mem
            var recordContext = new DefaultHttpContext();
            var recordController = new Record(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = recordContext
                }
            };
            await recordController.Start();
            var inMemId = recordContext.Response.Headers["x-recording-id"].ToString();
            recordContext.Request.Headers["x-recording-id"] = new string[] { inMemId };
            recordController.Stop();

            var playbackContext = new DefaultHttpContext();
            playbackContext.Request.Headers["x-recording-id"] = inMemId;
            var playbackController = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await playbackController.Start();
            var targetRecordingId = playbackContext.Response.Headers["x-recording-id"].ToString();
            playbackContext.Request.Headers["x-recording-id"] = new string[] { targetRecordingId };
            playbackController.Stop();

            testRecordingHandler.InMemorySessions.ContainsKey(targetRecordingId);
        }

        [Fact]
        public async Task TestPlaybackSetsRetryAfterToZero()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"Test.RecordEntries/response_with_retry_after.json\"}";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            Assert.False(String.IsNullOrEmpty(recordingId));
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(recordingId));
            var entry = testRecordingHandler.PlaybackSessions[recordingId].Session.Entries[0];
            HttpRequest request = TestHelpers.CreateRequestFromEntry(entry);
            HttpResponse response = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);
            Assert.Equal("0", response.Headers["Retry-After"]);

            // this response did not have the retry-after header initially, so it should not have been added.
            entry = testRecordingHandler.PlaybackSessions[recordingId].Session.Entries[0];
            request = TestHelpers.CreateRequestFromEntry(entry);
            response = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);
            Assert.False(response.Headers.ContainsKey("Retry-After"));
        }

        [Fact]
        public async Task TestPlaybackWithGZippedContentPlayback()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"Test.RecordEntries/request_response_with_gzipped_content.json\"}";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            Assert.False(String.IsNullOrEmpty(recordingId));
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(recordingId));
            var entry = testRecordingHandler.PlaybackSessions[recordingId].Session.Entries[0];
            HttpRequest request = TestHelpers.CreateRequestFromEntry(entry);

            // compress the body to simulate what the request coming from the library will look like
            request.Body = new MemoryStream(CompressionUtilities.CompressBody(BinaryData.FromStream(request.Body).ToArray(), request.Headers));
            HttpResponse response = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);
        }


        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets"",
              ""AssetsRepoPrefixPath"": """",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables_0f2a2fa5""
        }")]
        [Trait("Category", "Integration")]
        public async Task TestPlaybackWithRelativeAssetsJsonPath(string inputJson)
        {
            var folderStructure = new string[]
            {
                Path.Join("sdk", "tables", GitStoretests.AssetsJson)
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);

            try
            {
                RecordingHandler testRecordingHandler = new RecordingHandler(testFolder, new GitStore());
                var httpContext = new DefaultHttpContext();
                var recordingPath = "sdk/tables/azure-data-tables/tests/recordings/test_challenge_auth.pyTestTableChallengeAuthtest_challenge_auth_supported_version.json";
                var body = "{\"x-recording-file\":\"" + recordingPath + "\", \"x-recording-assets-file\": \"sdk/tables/assets.json\"}";
                httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
                httpContext.Request.ContentLength = httpContext.Request.Body.Length;

                var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
                {
                    ControllerContext = new ControllerContext()
                    {
                        HttpContext = httpContext
                    }
                };
                await controller.Start();
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }

        [Theory]
        [InlineData(
        @"{
              ""AssetsRepo"": ""Azure/azure-sdk-assets"",
              ""AssetsRepoPrefixPath"": """",
              ""AssetsRepoId"": """",
              ""TagPrefix"": ""python/tables"",
              ""Tag"": ""python/tables_0f2a2fa5""
        }")]
        [Trait("Category", "Integration")]
        public async Task TestPlaybackWithAbsoluteAssetsJsonPath(string inputJson)
        {
            var assetsJsonRelativePath = Path.Join("sdk", "tables", GitStoretests.AssetsJson);

            var folderStructure = new string[]
            {
                assetsJsonRelativePath
            };

            Assets assets = JsonSerializer.Deserialize<Assets>(inputJson);
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            var pathToAssetsJson = Path.Join(testFolder, assetsJsonRelativePath).Replace("\\", "/");

            try
            {
                RecordingHandler testRecordingHandler = new RecordingHandler(testFolder, new GitStore());
                var httpContext = new DefaultHttpContext();
                var recordingPath = "sdk/tables/azure-data-tables/tests/recordings/test_challenge_auth.pyTestTableChallengeAuthtest_challenge_auth_supported_version.json";
                var body = "{\"x-recording-file\":\"" + recordingPath + "\", \"x-recording-assets-file\": \"" + pathToAssetsJson + "\"}";
                httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
                httpContext.Request.ContentLength = httpContext.Request.Body.Length;

                var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
                {
                    ControllerContext = new ControllerContext()
                    {
                        HttpContext = httpContext
                    }
                };
                await controller.Start();
            }
            finally
            {
                DirectoryHelper.DeleteGitDirectory(testFolder);
            }
        }
    }
}
