// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Record : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly RecordingHandler _recordingHandler;

        public Record(RecordingHandler recordingHandler, ILoggerFactory loggerFactory)
        {
            _recordingHandler = recordingHandler;
            _logger = loggerFactory.CreateLogger<Record>();
        }

        [HttpPost]
        public async Task Start()
        {
            var body = await HttpRequestInteractions.GetBody(Request);

            if (body == null)
            {
                DebugLogger.LogAdminRequestDetails(_logger, Request);
                await _recordingHandler.StartRecordingAsync(null, Response, null);
            }
            else
            {
                string file = HttpRequestInteractions.GetBodyKey(body, "x-recording-file", allowNulls: false);
                var assetsJson = RecordingHandler.GetAssetsJsonLocation(
                    HttpRequestInteractions.GetBodyKey(body, "x-recording-assets-file", allowNulls: true),
                    _recordingHandler.ContextDirectory);

                DebugLogger.LogAdminRequestDetails(_logger, Request);
                _logger.LogDebug($"Attempting to start recording for {file} {assetsJson ?? string.Empty}");

                if (string.IsNullOrWhiteSpace(file))
                {
                    throw new HttpException(HttpStatusCode.BadRequest, "If providing a body to /Record/Start, the key 'x-recording-file' must be provided. If attempting to start an in-memory recording, provide NO body.");
                }

                await _recordingHandler.StartRecordingAsync(file, Response, assetsJson);

                if (Startup.ProxyConfiguration.Mode == UniversalRecordingMode.StandardRecord || Startup.ProxyConfiguration.Mode == UniversalRecordingMode.StandardPlayback)
                {
                    Startup.ProxyConfiguration.RecordingId = Response.Headers["x-recording-id"];
                    // we use Start() to transition from StandardPlayback to StandardRecord and vice-versa
                    Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
                }
            }
        }

        [HttpPost]
        public async Task Push()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            var options = await HttpRequestInteractions.GetBody<Dictionary<string, object>>(Request);

            var pathToAssets = RecordingHandler.GetAssetsJsonLocation(StoreResolver.ParseAssetsJsonBody(options), _recordingHandler.ContextDirectory);

            await _recordingHandler.Store.Push(pathToAssets);
        }

        [HttpPost]
        public async Task Stop()
        {
            string id = string.Empty;

            if (Startup.ProxyConfiguration.Mode == UniversalRecordingMode.StandardPlayback || Startup.ProxyConfiguration.Mode == UniversalRecordingMode.StandardRecord)
            {
                id = Startup.ProxyConfiguration.RecordingId;
            }
            else
            {
                id = RecordingHandler.GetHeader(Request, "x-recording-id");
            }

            var variables = await HttpRequestInteractions.GetBody<Dictionary<string, string>>(Request);

            bool save = true;
            EntryRecordMode mode = RecordingHandler.GetRecordMode(Request);

            if (mode != EntryRecordMode.Record && mode != EntryRecordMode.DontRecord)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "When stopping a recording and providing a \"x-recording-skip\" value, only value \"request-response\" is accepted.");
            }

            if (mode == EntryRecordMode.DontRecord)
            {
                save = false;
            }

            DebugLogger.LogAdminRequestDetails(_logger, Request);

            if (Startup.ProxyConfiguration.Mode == UniversalRecordingMode.StandardPlayback)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "The proxy is currently in playback mode. <proxyurl>/Record/Stop is not a valid operation in playback mode. Start a recording session using  /Record/Start before calling Record/Stop.");
            }
            if (Startup.ProxyConfiguration.Mode == UniversalRecordingMode.StandardRecord)
            {
                await _recordingHandler.StopRecording(Startup.ProxyConfiguration.RecordingId, variables: variables, saveRecording: save);
                Startup.ProxyConfiguration.RecordingId = null;
            }
            else
            {
                await _recordingHandler.StopRecording(id, variables: variables, saveRecording: save);
            }
        }

        public async Task HandleRequest()
        {
            string id = string.Empty;
            if (Startup.ProxyConfiguration.Mode == UniversalRecordingMode.Azure)
            {
                id = RecordingHandler.GetHeader(Request, "x-recording-id");
            }
            else
            {
                id = Startup.ProxyConfiguration.RecordingId;
            }
            await _recordingHandler.HandleRecordRequestAsync(id, Request, Response);
        }
    }
}
