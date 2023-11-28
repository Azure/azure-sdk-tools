// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Playback : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly RecordingHandler _recordingHandler;

        public Playback(RecordingHandler recordingHandler, ILoggerFactory loggerFactory)
        {
            _recordingHandler = recordingHandler;
            _logger = loggerFactory.CreateLogger<Playback>();
        }

        [HttpPost]
        public async Task Start()
        {
            var body = await HttpRequestInteractions.GetBody(Request);

            string file = HttpRequestInteractions.GetBodyKey(body, "x-recording-file", allowNulls: true);
            string recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);
            var assetsJson = RecordingHandler.GetAssetsJsonLocation(
                HttpRequestInteractions.GetBodyKey(body, "x-recording-assets-file", allowNulls: true),
                _recordingHandler.ContextDirectory);

            DebugLogger.LogAdminRequestDetails(_logger, Request);
            _logger.LogDebug($"Attempting to start recording for {file??"In-Memory Recording"} {assetsJson ?? string.Empty}");

            if (String.IsNullOrEmpty(file) && !String.IsNullOrEmpty(recordingId))
            {
                await _recordingHandler.StartPlaybackAsync(recordingId, Response, RecordingType.InMemory, assetsJson);
            }
            else if(!String.IsNullOrEmpty(file))
            {
                await _recordingHandler.StartPlaybackAsync(file, Response, RecordingType.FilePersisted, assetsJson);
            }
            else
            {
                throw new HttpException(HttpStatusCode.BadRequest, "At least one of either JSON body key 'x-recording-file' or header 'x-recording-id' must be populated when starting playback.");
            }
        }

        [HttpPost]
        public void Stop()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);

            string id = RecordingHandler.GetHeader(Request, "x-recording-id");
            bool.TryParse(RecordingHandler.GetHeader(Request, "x-purge-inmemory-recording", true), out var shouldPurgeRecording);

            _recordingHandler.StopPlayback(id, purgeMemoryStore: shouldPurgeRecording);
        }

        [HttpPost]
        public async Task Reset([FromBody()] IDictionary<string, object> options = null)
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);

            var pathToAssets = RecordingHandler.GetAssetsJsonLocation(StoreResolver.ParseAssetsJsonBody(options), _recordingHandler.ContextDirectory);

            await _recordingHandler.Store.Reset(pathToAssets);
        }

        [HttpPost]
        public async Task Restore([FromBody()] IDictionary<string, object> options = null)
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);

            var pathToAssets = RecordingHandler.GetAssetsJsonLocation(StoreResolver.ParseAssetsJsonBody(options), _recordingHandler.ContextDirectory);

            await _recordingHandler.Restore(pathToAssets);
        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandlePlaybackRequest(id, Request, Response);
        }

    }
}
