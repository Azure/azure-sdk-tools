// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Core.Pipeline;
using System;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RemoteRecordTransport : MockTransport
    {
        // !! TODO: Hardcoded
        // !! TODO: Relies on dev cert
        private static readonly Uri s_recordingServerUri = new Uri("https://localhost:5001");
        private static readonly Uri s_startPlaybackUri = new Uri(s_recordingServerUri, "/playback/start");
        private static readonly Uri s_stopPlaybackUri = new Uri(s_recordingServerUri, "/playback/stop");
        private static readonly Uri s_startRecordUri = new Uri(s_recordingServerUri, "/record/start");
        private static readonly Uri s_stopRecordUri = new Uri(s_recordingServerUri, "/record/stop");
        private static readonly ResponseClassifier s_classifier = new ResponseClassifier();

        private readonly string _sessionFile;
        private readonly HttpPipelineTransport _transport;
        private string _recordingId;
        private Uri _startUri;
        private Uri _stopUri;
        private string _mode;

        public RemoteRecordTransport(HttpPipelineTransport transport, string sessionFile, bool playback)
        {
            _transport = transport;
            _sessionFile = sessionFile;
            _startUri = playback ? s_startPlaybackUri : s_startRecordUri;
            _stopUri = playback ? s_stopPlaybackUri : s_stopRecordUri;
            _mode = playback ? "playback" : "record";
        }

        public override Request CreateRequest()
        {
            return _transport.CreateRequest();
        }

        public override void Process(HttpMessage message)
        {
            Start();
            Redirect(message.Request);
            _transport.Process(message);
        }

        public async override ValueTask ProcessAsync(HttpMessage message)
        {
            await StartAsync().ConfigureAwait(false);
            Redirect(message.Request);
            await _transport.ProcessAsync(message).ConfigureAwait(false);
        }

        public void Start()
        {
            if (_recordingId == null)
            {
                var startRequest = CreateRecordingRequest(_startUri);
                _transport.Process(startRequest);
                ProcessStartResponse(startRequest.Response);
            }
        }

        public async ValueTask StartAsync()
        {
            if (_recordingId == null)
            {
                var startRequest = CreateRecordingRequest(_startUri);
                await _transport.ProcessAsync(startRequest).ConfigureAwait(false);
                ProcessStartResponse(startRequest.Response);
            }
        }

        public void Stop(bool save = true)
        {
            if (_recordingId != null)
            {
                var stopRequest = CreateRecordingRequest(_stopUri);
                stopRequest.Request.Headers.Add("x-recording-save", save.ToString());
                _transport.Process(stopRequest);
            }
        }

        private void ProcessStartResponse(Response response)
        {
            if (response.Status != 200)
            {
                throw new InvalidOperationException("Start request failed.");
            }

            if (!response.Headers.TryGetValue("x-recording-id", out var id))
            {
                throw new InvalidOperationException("No recording ID returned for start request.");
            }

            _recordingId = id;
        }

        private HttpMessage CreateRecordingRequest(Uri uri)
        {
            var request = _transport.CreateRequest();
            request.Method = RequestMethod.Post;
            request.Uri.Reset(uri);
            request.Headers.SetValue("x-recording-file", _sessionFile);

            if (_recordingId != null)
            {
                request.Headers.SetValue("x-recording-id", _recordingId);
            }

            return new HttpMessage(request, s_classifier);
        }

        private void Redirect(Request request)
        {
            if (request.Headers.Contains("x-recording-id"))
            {
                // We have returned here on retry and we'll set the wrong
                // upstream base URI if we continue.
                return;
            }

            request.Headers.SetValue("x-recording-id", _recordingId);
            request.Headers.SetValue("x-recording-mode", _mode);
            request.Headers.SetValue("x-recording-upstream-base-uri", GetUpstreamBaseUri(request));
            request.Uri.Scheme = s_recordingServerUri.Scheme;
            request.Uri.Host = s_recordingServerUri.Host;
            request.Uri.Port = s_recordingServerUri.Port;
        }

        private string GetUpstreamBaseUri(Request request)
        {
            var builder = new RequestUriBuilder();
            builder.Reset(request.Uri.ToUri());
            builder.Path = "";
            builder.Query = "";
            return builder.ToString();
        }
    }
}
