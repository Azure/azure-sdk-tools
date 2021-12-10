// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Playback : ControllerBase
    {
        private readonly RecordingHandler _recordingHandler;
        public Playback(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;

        [HttpPost]
        public async Task Start()
        {
            string file = RecordingHandler.GetHeader(Request, "x-recording-file", true);
            string recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", true);

            if (String.IsNullOrEmpty(file) && !String.IsNullOrEmpty(recordingId))
            {
                await _recordingHandler.StartPlayback(recordingId, Response, RecordingType.InMemory);
            }
            else if(!String.IsNullOrEmpty(file))
            {
                await _recordingHandler.StartPlayback(file, Response, RecordingType.FilePersisted);
            }
            else
            {
                throw new HttpException(HttpStatusCode.BadRequest, "At least one of two headers 'x-recording-file' or 'x-recording-id' must be set when starting playback.");
            }
        }

        [HttpPost]
        public void Stop()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");
            bool.TryParse(RecordingHandler.GetHeader(Request, "x-purge-inmemory-recording", true), out var shouldPurgeRecording);

            _recordingHandler.StopPlayback(id, purgeMemoryStore: shouldPurgeRecording);
        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandlePlaybackRequest(id, Request, Response);
        }

    }
}
