using System.Net.Http;
using NUnit.Framework;

namespace Azure.Sdk.Tools.TestProxy.TestExample
{
    /// <summary>
    /// This class is an example integration with the test proxy. This particular implementation assumes that the test-proxy is already running
    /// for brevity. A user's test framework would likely spin up and spin down the test-proxy process as the .NET team does.
    /// 
    /// Reference https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core.TestFramework/src/TestProxy.cs#L63 for the full implementation.
    /// </summary>
    public class Tests
    {
        private const string _proxyurl = "https://localhost:5001";

        // For your test client, you can either maintain the lack of certificate validation (the test-proxy is making real HTTPS calls, so if your actual api call
        // is having cert issues, those will still surface.
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        /// <summary>
        /// After a recording or playback has started, the recordingId must be present in every successive request to be properly registered with the recording.
        /// </summary>
        public string RecordingId { get; set; }

        /// <summary>
        /// The this path is the path to the test file from the current test-proxy context. EG:
        /// 
        /// "proxy context" + "path/to/file.json"
        /// 
        /// While a user's repo may have a longer path to the recording location, it is not absolute necessary. For this example
        /// we will return path "recordings/TestClass/TestName.json"
        /// </summary>
        public string CurrentRecordingPath { get
            {
                return "";
            }
        }

        /// <summary>
        /// Each test must start their own individual test-proxy recording session. Either "record" or "playback".
        /// </summary>
        [SetUp]
        public void Start()
        {

        }

        /// <summary>
        /// Each test must also _stop_ their own individual recording session. During playback, an unstopped playback is just a resource drain on the server.
        /// During _recording_ however, 
        /// </summary>
        [TearDown]
        public void Stop()
        {

        }

        [Test]
        public void BasicTest()
        {
            Assert.Pass();
        }

        [Test]
        public void TestWithSanitizers()
        {
            Assert.Pass();
        }

        [Test]
        public void TestWithMatcher()
        {
            Assert.Pass();
        }
    }
}
