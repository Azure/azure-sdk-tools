using Microsoft.AspNetCore.Http;
using System.Linq;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordingHandlerTests
    {
        [Fact]
        public async void TestInMemoryPurgesSucessfully()
        {
            var recordingHandler = TestHelpers.LoadRecordSessionIntoInMemoryStore("Test.RecordEntries/post_delete_get_content.json");
            var httpContext = new DefaultHttpContext();
            var key = recordingHandler.InMemorySessions.Keys.First();

            await recordingHandler.StartPlayback(key, httpContext.Response, Common.RecordingType.InMemory);
            var playbackSession = httpContext.Response.Headers["x-recording-id"];
            recordingHandler.StopPlayback(playbackSession, true);

            Assert.True(0 == recordingHandler.InMemorySessions.Count);
        }

        [Fact]
        public async void TestInMemoryDoesntPurgeErroneously()
        {
            var recordingHandler = TestHelpers.LoadRecordSessionIntoInMemoryStore("Test.RecordEntries/post_delete_get_content.json");
            var httpContext = new DefaultHttpContext();
            var key = recordingHandler.InMemorySessions.Keys.First();

            await recordingHandler.StartPlayback(key, httpContext.Response, Common.RecordingType.InMemory);
            var playbackSession = httpContext.Response.Headers["x-recording-id"];
            recordingHandler.StopPlayback(playbackSession, false);

            Assert.True(1 == recordingHandler.InMemorySessions.Count);
        }
    }
}
