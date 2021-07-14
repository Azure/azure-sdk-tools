// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Record : ControllerBase
    {
        private readonly RecordingHandler _recordingHandler;
        public Record(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;


        private static readonly HttpClient s_client = new HttpClient() { Timeout = TimeSpan.FromSeconds(600) };

        [HttpPost]
        [HttpOptions]
        [EnableCors]
        public void Start()
        {
            if(String.Equals(Request.Method, "options", StringComparison.OrdinalIgnoreCase)){
                Response.Headers.Add("Allow", "POST");
                Response.Headers.Add("Access-Control-Allow-Origin", "*");
                Response.Headers.Add("Access-Control-Allow-Methods", "POST,OPTIONS");
                Response.Headers.Add("Access-Control-Allow-Headers", "*");
            }
            else
            {
                string file = RecordingHandler.GetHeader(Request, "x-recording-file", allowNulls: true);

                _recordingHandler.StartRecording(file, Response);
            }
        }

        [HttpPost]
        [EnableCors]
        public void Stop()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            _recordingHandler.StopRecording(id);

        }

        [EnableCors]
        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequest(id, Request, Response, s_client);
        }
    }
}
