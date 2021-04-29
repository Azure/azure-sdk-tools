// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Record : ControllerBase
    {
        private readonly RecordingHandler _recordingHandler;
        public Record(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;


        private static readonly HttpClient s_client = new HttpClient();

        [HttpPost]
        public void Start()
        {
            string file = RecordingHandler.GetHeader(Request, "x-recording-file");

            _recordingHandler.StartRecording(file, Response);
        }

        [HttpPost]
        public void Stop()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");
            bool save = bool.Parse(RecordingHandler.GetHeader(Request, "x-recording-save"));

            _recordingHandler.StopRecording(id, save);

        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequest(id, Request, Response, s_client);
        }
    }
}
