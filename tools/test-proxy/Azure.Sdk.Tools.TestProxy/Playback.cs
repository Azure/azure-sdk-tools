// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
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
            string file = await HttpRequestInteractions.GetBodyKey(Request, "x-recording-file", true);
            string recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", true);

            if (String.IsNullOrEmpty(file) && !String.IsNullOrEmpty(recordingId))
            {
                await _recordingHandler.StartPlaybackAsync(recordingId, Response, RecordingType.InMemory);
            }
            else if(!String.IsNullOrEmpty(file))
            {
                await _recordingHandler.StartPlaybackAsync(file, Response, RecordingType.FilePersisted);
            }
            else
            {
                throw new HttpException(HttpStatusCode.BadRequest, "At least one of either JSON body key 'x-recording-file' or header 'x-recording-id' must be populated when starting playback.");
            }
        }

        [HttpPost]
        public void Stop()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");
            bool.TryParse(RecordingHandler.GetHeader(Request, "x-purge-inmemory-recording", true), out var shouldPurgeRecording);

            _recordingHandler.StopPlayback(id, purgeMemoryStore: shouldPurgeRecording);
        }


        [HttpPost]
        public async Task Reset([FromBody()] IDictionary<string, object> options = null)
        {
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);

            var pathToAssets = StoreResolver.ParseAssetsJsonBody(options);

            _recordingHandler.Store.Reset(pathToAssets, _recordingHandler.ContextDirectory);
        }

        [HttpPost]
        public async Task Restore([FromBody()] IDictionary<string, object> options = null)
        {
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);

            var pathToAssets = StoreResolver.ParseAssetsJsonBody(options);

            _recordingHandler.Store.Restore(pathToAssets, _recordingHandler.ContextDirectory);
        }


        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandlePlaybackRequest(id, Request, Response);
        }

    }
}
