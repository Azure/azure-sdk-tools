// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        public async Task Start()
        {
            string file = await HttpRequestInteractions.GetBodyKey(Request, "x-recording-file", allowNulls: true);

            _recordingHandler.StartRecording(file, Response);
        }

        [HttpPost]
        [AllowEmptyBody]
        public void Stop([FromBody()] IDictionary<string, string> variables = null)
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            _recordingHandler.StopRecording(id, variables: variables);
        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequestAsync(id, Request, Response, s_client);
        }
    }
}
