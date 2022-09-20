using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.HttpClientSample
{
    class Program
    {
        private const string _url = "https://www.example.org";
        private const string _proxy = "https://localhost:5001";
        private static readonly string _recordingFile = "recordings/net-http-client-sample.json";

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        static async Task Main(string[] args)
        {
            Console.WriteLine($"Recording File: {_recordingFile}");
            Console.WriteLine();

            await Record();
            await Playback();
        }

        private static async Task Record()
        {
            var recordingId = await StartRecording();
            await SendRequest(recordingId, "record");
            await Task.Delay(TimeSpan.FromSeconds(2));
            await SendRequest(recordingId, "record");
            await StopRecording(recordingId);
        }

        private static async Task Playback()
        {
            var recordingId = await StartPlayback();
            await SendRequest(recordingId, "playback");
            await SendRequest(recordingId, "playback");
            await StopPlayback(recordingId);
        }

        private static async Task<string> StartPlayback()
        {
            Console.WriteLine("StartPlayback");

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "/playback/start");

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

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "/playback/stop");
            message.Headers.Add("x-recording-id", recordingId);

            await _httpClient.SendAsync(message);
        }

        private static async Task<string> StartRecording()
        {
            Console.WriteLine("StartRecording");

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "/record/start");

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

            var message = new HttpRequestMessage(HttpMethod.Post, _proxy + "/record/stop");
            message.Headers.Add("x-recording-id", recordingId);
            message.Headers.Add("x-recording-save", bool.TrueString);

            await _httpClient.SendAsync(message);
        }

        private static async Task SendRequest(string recordingId, string mode)
        {
            Console.WriteLine("Request");

            var message = new HttpRequestMessage(HttpMethod.Get, _proxy);
            message.Headers.Add("x-recording-id", recordingId);
            message.Headers.Add("x-recording-mode", mode);
            message.Headers.Add("x-recording-upstream-base-uri", _url);

            var response = await _httpClient.SendAsync(message);
            var body = (await response.Content.ReadAsStringAsync());

            Console.WriteLine("Headers:");
            Console.WriteLine($"  Date: {response.Headers.Date.Value.LocalDateTime}");
            Console.WriteLine($"Body: {(body.Replace("\r", string.Empty).Replace("\n", string.Empty).Substring(0, 80))}");
            Console.WriteLine();
        }
    }
}
