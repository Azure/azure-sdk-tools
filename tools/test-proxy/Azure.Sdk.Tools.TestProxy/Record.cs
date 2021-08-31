// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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


        private static readonly HttpClient s_client = Startup.Insecure ?
            new HttpClient(new HttpClientHandler() {  ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
            {
                Timeout = TimeSpan.FromSeconds(600)
            } :
            new HttpClient() {
                Timeout = TimeSpan.FromSeconds(600)
            };

        [HttpPost]
        public void Start()
        {
            string file = RecordingHandler.GetHeader(Request, "x-recording-file", allowNulls: true);

            _recordingHandler.StartRecording(file, Response);
        }

        [HttpPost]
        public void Stop()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            _recordingHandler.StopRecording(id);

        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequest(id, Request, Response, s_client);
        }
    }
}
