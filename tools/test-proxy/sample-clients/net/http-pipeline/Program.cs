using Azure.Core;
using Azure.Core.Pipeline;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.HttpPipelineSample
{
    class Program
    {
        private static readonly Uri _url = new Uri("https://www.example.org");
        private static readonly Uri _proxy = new Uri("https://localhost:5001");
        private static readonly string _recordingFile = Path.Combine("test-proxy", "net-http-pipeline-sample.json");

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        static async Task Main(string[] args)
        {
            Console.WriteLine($"Recording File: {_recordingFile}");
            Console.WriteLine();

            var defaultPipeline = HttpPipelineBuilder.Build(new TestClientOptions());
            await SendRequest(defaultPipeline);

            await Record();
            await Playback();
        }

        private static async Task Record()
        {
            var recordingId = await StartRecording();

            var pipeline = HttpPipelineBuilder.Build(new TestClientOptions()
            {
                Transport = new TestProxyTransport(new HttpClientTransport(_httpClient), _proxy.Host, _proxy.Port, recordingId, "record"),
            });

            await SendRequest(pipeline);
            await Task.Delay(TimeSpan.FromSeconds(2));
            await SendRequest(pipeline);
            
            await StopRecording(recordingId);
        }

        private static async Task Playback()
        {
            var recordingId = await StartPlayback();

            var pipeline = HttpPipelineBuilder.Build(new TestClientOptions()
            {
                Transport = new TestProxyTransport(new HttpClientTransport(_httpClient), _proxy.Host, _proxy.Port, recordingId, "playback"),
            });

            await SendRequest(pipeline);
            await SendRequest(pipeline);
            
            await StopPlayback(recordingId);
        }

        private static async Task<string> StartPlayback()
        {
            Console.WriteLine("StartPlayback");

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "playback/start");
            message.Headers.Add("x-recording-file", _recordingFile);

            var response = await _httpClient.SendAsync(message);
            var recordingId = response.Headers.GetValues("x-recording-id").Single();
            Console.WriteLine($"  x-recording-id: {recordingId}");
            Console.WriteLine();

            return recordingId;
        }

        private static async Task StopPlayback(string recordingId)
        {
            Console.WriteLine("StopPlayback");
            Console.WriteLine();

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "playback/stop");
            message.Headers.Add("x-recording-id", recordingId);

            await _httpClient.SendAsync(message);
        }

        private static async Task<string> StartRecording()
        {
            Console.WriteLine("StartRecording");

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "record/start");
            message.Headers.Add("x-recording-file", _recordingFile);

            var response = await _httpClient.SendAsync(message);
            var recordingId = response.Headers.GetValues("x-recording-id").Single();
            Console.WriteLine($"  x-recording-id: {recordingId}");
            Console.WriteLine();

            return recordingId;
        }

        private static async Task StopRecording(string recordingId)
        {
            Console.WriteLine("StopRecording");
            Console.WriteLine();

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "record/stop");
            message.Headers.Add("x-recording-id", recordingId);
            message.Headers.Add("x-recording-save", bool.TrueString);

            await _httpClient.SendAsync(message);
        }

        private static async Task SendRequest(HttpPipeline pipeline)
        {
            Console.WriteLine("Request");

            var message = pipeline.CreateMessage();
            message.Request.Uri.Reset(_url);

            await pipeline.SendAsync(message, CancellationToken.None);
            var body = (new StreamReader(message.Response.ContentStream)).ReadToEnd();

            Console.WriteLine("Headers:");
            Console.WriteLine($"  Date: {message.Response.Headers.Date.Value.LocalDateTime}");
            Console.WriteLine($"Body: {(body.Replace("\r", string.Empty).Replace("\n", string.Empty).Substring(0, 80))}");
            Console.WriteLine();
        }

        private class TestProxyTransport : HttpPipelineTransport
        {
            private readonly HttpPipelineTransport _transport;
            private readonly string _host;
            private readonly int? _port;
            private readonly string _recordingId;
            private readonly string _mode;
            
            public TestProxyTransport(HttpPipelineTransport transport, string host, int? port, string recordingId, string mode)
            {
                _transport = transport;
                _host = host;
                _port = port;
                _recordingId = recordingId;
                _mode = mode;
            }

            public override Request CreateRequest()
            {
                return _transport.CreateRequest();
            }

            public override void Process(HttpMessage message)
            {
                RedirectToTestProxy(message);
                _transport.Process(message);
            }

            public override ValueTask ProcessAsync(HttpMessage message)
            {
                RedirectToTestProxy(message);
                return _transport.ProcessAsync(message);
            }

            private void RedirectToTestProxy(HttpMessage message)
            {
                message.Request.Headers.Add("x-recording-id", _recordingId);
                message.Request.Headers.Add("x-recording-mode", _mode);

                var baseUri = new RequestUriBuilder() {
                    Scheme = message.Request.Uri.Scheme,
                    Host = message.Request.Uri.Host,
                    Port = message.Request.Uri.Port,
                };
                message.Request.Headers.Add("x-recording-upstream-base-uri", baseUri.ToString());

                message.Request.Uri.Host = _host;
                if (_port.HasValue)
                {
                    message.Request.Uri.Port = _port.Value;
                }
            }
        }

        private class TestClientOptions : ClientOptions { }
    }
}