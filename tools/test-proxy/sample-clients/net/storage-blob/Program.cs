using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.StorageBlobSample
{
    class Program
    {
        private static readonly string _connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
        private static readonly string _containerName = "net-storage-blob-sample" + Guid.NewGuid().ToString();
        private const string _blobName = "sample";

        private static readonly Uri _proxy = new Uri("https://localhost:5001");
        private static readonly string _recordingFile = "recordings/net-storage-blob-sample.json";

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        private static readonly HttpPipelineTransport _httpClientTransport = new HttpClientTransport(_httpClient);

        static async Task Main(string[] args)
        {
            Console.WriteLine($"Recording File: {_recordingFile}");
            Console.WriteLine();

            var serviceClient = new BlobServiceClient(_connectionString);
            var containerClient = serviceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(_blobName);

            try
            {
                // Create container and upload blob
                await containerClient.CreateIfNotExistsAsync();
                await blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes("sample")));

                // Download blob directly from service
                await SendRequest(_httpClientTransport);

                await Record();

                await Playback();
            }
            finally
            {
                await containerClient.DeleteIfExistsAsync();
            }
        }

        private static async Task Record()
        {
            var recordingId = await StartRecording();

            var transport = new TestProxyTransport(new HttpClientTransport(_httpClient), _proxy.Host, _proxy.Port, recordingId, "record");

            await SendRequest(transport);
            await Task.Delay(TimeSpan.FromSeconds(2));
            await SendRequest(transport);

            await StopRecording(recordingId);
        }

        private static async Task Playback()
        {
            var recordingId = await StartPlayback();

            var transport = new TestProxyTransport(new HttpClientTransport(_httpClient), _proxy.Host, _proxy.Port, recordingId, "playback");

            await SendRequest(transport);
            await SendRequest(transport);

            await StopPlayback(recordingId);
        }

        private static async Task<string> StartPlayback()
        {
            Console.WriteLine("StartPlayback");

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "playback/start");
            var json = "{\"x-recording-file\":\"" + _recordingFile + "\"}";
            var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
            message.Content = content;

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
            var json = "{\"x-recording-file\":\"" + _recordingFile + "\"}";
            var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
            message.Content = content;

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

        private static async Task SendRequest(HttpPipelineTransport transport)
        {
            Console.WriteLine("Request");

            var serviceClient = new BlobServiceClient(_connectionString, new BlobClientOptions()
            {
                Transport = transport,
            });
            var containerClient = serviceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(_blobName);

            var stream = new MemoryStream();
            var response = await blobClient.DownloadToAsync(stream);

            Console.WriteLine("Headers:");
            Console.WriteLine($"  Date: {response.Headers.Date.Value.LocalDateTime}");
            Console.WriteLine($"Content: {Encoding.UTF8.GetString(stream.ToArray())}");
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
                message.Request.Uri.Scheme = _proxy.Scheme;
                if (_port.HasValue)
                {
                    message.Request.Uri.Port = _port.Value;
                }
            }
        }

        private class TestClientOptions : ClientOptions { }
    }
}
