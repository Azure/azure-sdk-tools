// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

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

        // when doing anything in the universal mode (name pending feedback), we need to use a static recording id
        // so that we can still tie to a specific recording even if the client has no idea what the id should be.
        internal static string UniversalRecordingId; // made internal so Startup can read it

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
                _logger.LogDebug($"Attempting to start recording for {file} {assetsJson??string.Empty}");

                if (string.IsNullOrWhiteSpace(file))
                {
                    throw new HttpException(HttpStatusCode.BadRequest, "If providing a body to /Record/Start, the key 'x-recording-file' must be provided. If attempting to start an in-memory recording, provide NO body.");
                }

                await _recordingHandler.StartRecordingAsync(file, Response, assetsJson);
            }
        }

        [HttpPost]
        public async Task StartUniversal()
        {
            if (!Startup.StandardProxyMode)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "The /Record/StartUniversal endpoint is only available when the proxy is started in 'standard' mode. Re-run the proxy with --standard-proxy-mode.");
            }

            DebugLogger.LogAdminRequestDetails(_logger, Request);

            // If already active just return existing id so clients can discover it.
            if (!string.IsNullOrEmpty(UniversalRecordingId))
            {
                if (!Response.Headers.ContainsKey("x-recording-id"))
                {
                    Response.Headers["x-recording-id"] = UniversalRecordingId;
                }
                return;
            }

            await _recordingHandler.StartRecordingAsync(null, Response, null);
            UniversalRecordingId = Response.Headers["x-recording-id"].ToString();
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
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

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

            await _recordingHandler.StopRecording(id, variables: variables, saveRecording: save);
        }

        [HttpPost]
        public async Task StopUniversal()
        {
            if (!Startup.StandardProxyMode)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "The /Record/StopUniversal endpoint is only available when the proxy is started in 'standard' mode. Re-run the proxy with --standard-proxy-mode.");
            }

            if (string.IsNullOrEmpty(UniversalRecordingId))
            {
                return; // nothing active
            }

            DebugLogger.LogAdminRequestDetails(_logger, Request);
            await _recordingHandler.StopRecording(UniversalRecordingId);
            UniversalRecordingId = null;
        }


        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequestAsync(id, Request, Response);
        }
    }
}
