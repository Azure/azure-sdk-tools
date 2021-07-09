using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.StorageBlobSample
{
    /// <summary>
    /// This program gives a sample of what it looks like to allow recording of a chunked upload and download.
    /// </summary>
    class Program
    {
        private static readonly string _connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
        private static readonly string _containerName = "net-chunked-transfer-sample" + Guid.NewGuid().ToString();
        private const string _blobName = "sample";

        private static readonly Uri _proxy = new Uri("https://localhost:5001");
        private static readonly string _recordingFile = Path.Combine("test-proxy", "net-storage-blob-sample.json");

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

            try
            {
                // Create container and upload blob
                await containerClient.CreateIfNotExistsAsync();

                await Record(containerClient);

                await Playback(containerClient);
            }
            finally
            {
                await containerClient.DeleteIfExistsAsync();
            }
        }

        private static string GenerateLargeTextFile()
        {
            var tempFileLocation = Path.Combine(Path.GetTempPath(), "large-sample-file.txt");

            using (StreamWriter file = new(tempFileLocation))
            {
                for (int i = 0; i < 1000000; i++)
                {
                    file.Write(i.ToString() + " ");
                }
            }

            return tempFileLocation;
        }

        private async static Task UploadBlobsInChunks(BlobContainerClient containerClient)
        {

            var filePath = GenerateLargeTextFile();

            var blockBlobClient = containerClient.GetBlockBlobClient(_blobName);
            int blockSize = 1 * 1024 * 1024;//1 MB Block
            int offset = 0;
            int counter = 0;
            List<string> blockIds = new List<string>();

            using (var fs = File.OpenRead(filePath))
            {
                var bytesRemaining = fs.Length;
                do
                {
                    var dataToRead = Math.Min(bytesRemaining, blockSize);
                    byte[] data = new byte[dataToRead];
                    var dataRead = fs.Read(data, offset, (int)dataToRead);
                    bytesRemaining -= dataRead;
                    if (dataRead > 0)
                    {
                        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(counter.ToString("d6")));
                        await blockBlobClient.StageBlockAsync(blockId, new MemoryStream(data));
                        Console.WriteLine(string.Format("Block {0} uploaded successfully.", counter.ToString("d6")));
                        blockIds.Add(blockId);
                        counter++;
                    }
                }
                while (bytesRemaining > 0);
                Console.WriteLine("All blocks uploaded. Now committing block list.");
                var headers = new BlobHttpHeaders()
                {
                    ContentType = "text/plain"
                };
                blockBlobClient.CommitBlockList(blockIds, headers);
                Console.WriteLine("Blob uploaded successfully!");
            }
        }

        private static async Task SendRequest(HttpPipelineTransport transport)
        {
            Console.WriteLine("Request");

            var serviceClient = new BlobServiceClient(_connectionString, new BlobClientOptions()
            {
                Transport = transport,
            });
            var containerClient = serviceClient.GetBlobContainerClient(_containerName);
            await UploadBlobsInChunks(containerClient);
            Console.WriteLine();
        }

        private static async Task Record(BlobContainerClient containerClient)
        {
            var recordingId = await StartRecording();

            var transport = new TestProxyTransport(new HttpClientTransport(_httpClient), _proxy.Host, _proxy.Port, recordingId, "record");

            await SendRequest(transport);

            await StopRecording(recordingId);
        }

        private static async Task Playback(BlobContainerClient containerClient)
        {
            var recordingId = await StartPlayback();

            var transport = new TestProxyTransport(new HttpClientTransport(_httpClient), _proxy.Host, _proxy.Port, recordingId, "playback");

            await SendRequest(transport);

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