using System.Net.Http;
using NUnit.Framework;
using Azure.Sdk.Tools.TestProxy.HttpPipelineSample;
using Azure.Core.Pipeline;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Azure.Core;

namespace Azure.Sdk.Tools.TestProxy.TestExample
{
    /// <summary>
    /// This class is an example integration with the test proxy. This particular implementation assumes that the test-proxy is already running
    /// for brevity. A user's test framework would likely spin up and spin down the test-proxy process as the .NET team does.
    /// 
    /// 
    /// 
    /// Reference https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core.TestFramework/src/TestProxy.cs#L63 for a full implementation in a complex environment.
    /// </summary>
    public class SampleTestBaseClass
    {
        private const string RECORD_MODE = "playback";
        private const int PROXY_PORT = 5001;
        private const string PROXY_HOST = "localhost";

        /// <summary>
        /// For this and any test that will redirect to the test-proxy, the user should be provided a client that can actually carry out the request. In the case
        /// of adding tests for an existing library/class, the developer should pass this pipeline INTO their client. The key is that each request is dynamically modified
        /// to:
        /// 
        /// - Update uri to localhost so test-proxy gets the traffic.
        /// - Add x-recording-BLAH headers as necessary for the proxy actually complete the requests.
        /// - Add x-recording-id header so the proxy knows which session it is interacting with.
        /// 
        /// </summary>
        public HttpPipeline RequestPipeline { get; set; }

        // For your test client, you can either maintain the lack of certificate validation (the test-proxy is making real HTTPS calls, so if your actual api call
        // is having cert issues, those will still surface.
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        /// <summary>
        /// After a recording or playback has started, the recordingId must be present in every successive request to be properly registered with the recording.
        /// </summary>
        public string? RecordingId { get; set; }

        /// <summary>
        /// The this path is the path to the test file from the current test-proxy context. EG:
        /// 
        /// "proxy context" + "path/to/file.json"
        /// 
        /// While a user's repo may have a longer path to the recording location, it is not absolute necessary. For this example
        /// we will return path "recordings/TestName.json"
        /// </summary>
        public string CurrentRecordingPath { get
            {
                return Path.Join("recordings", $"{TestContext.CurrentContext.Test.Name}.json");
            }
        }

        /// <summary>
        /// Because environment variables set in debug pane of VS doesn't work well, we share mode selection.
        /// 
        /// When running from VS, set the RECORD_MODE variable manually.
        /// When running from CLI, set RECORD_MODE environment variable to "record" or "playback".
        /// </summary>
        public string CurrentPlaybackMode { get
            {
                var envRecordMode = Environment.GetEnvironmentVariable("RECORD_MODE");

                if (!string.IsNullOrWhiteSpace(envRecordMode)){
                    return envRecordMode;
                }
                else
                {
                    return RECORD_MODE;
                }
            }
        }

        /// <summary>
        /// Each test must start their own individual test-proxy recording session. Either "record" or "playback".
        /// </summary>
        [SetUp]
        public async Task Start()
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"https://{PROXY_HOST}:{PROXY_PORT}/{CurrentPlaybackMode}/start");

            message.Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, string>()
            {
                { "x-recording-file", CurrentRecordingPath }
            }), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(message);
            RecordingId = response.Headers.GetValues("x-recording-id").Single();

            // override our transport for current test, always ensuring we inject the correct recording id
            RequestPipeline = HttpPipelineBuilder.Build(new TestClientOptions()
            {
                Transport = new TestProxyTransport(new HttpClientTransport(_httpClient), PROXY_HOST, PROXY_PORT, RecordingId, CurrentPlaybackMode),
            });
        }

        /// <summary>
        /// Each test must also _stop_ their own individual recording session. During playback, an unstopped playback is just a resource drain on the server.
        /// During _recording_ however, failing to stop a recording will result in the recording NOT BEING SAVED.
        /// </summary>
        [TearDown]
        public async Task Stop()
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"https://{PROXY_HOST}:{PROXY_PORT}/{CurrentPlaybackMode}/stop");
            message.Headers.Add("x-recording-id", RecordingId);
            message.Headers.Add("x-recording-save", bool.TrueString);

            await _httpClient.SendAsync(message);
        }
    }

    public class TestClientOptions : ClientOptions { };
}
