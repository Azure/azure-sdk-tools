// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Record : ControllerBase
    {
        private readonly ILogger _logger;

        private readonly RecordingHandler _recordingHandler;

        private static readonly HttpClient RedirectableClient = Startup.Insecure ?
            new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
            {
                Timeout = TimeSpan.FromSeconds(600),
            } :
            new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(600)
            };

        private static readonly HttpClient RedirectlessClient = Startup.Insecure ?
            new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
            {
                Timeout = TimeSpan.FromSeconds(600),
            } :
            new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(600)
            };

        public Record(RecordingHandler recordingHandler, ILoggerFactory loggerFactory)
        {
            _recordingHandler = recordingHandler;
            _logger = loggerFactory.CreateLogger<Record>();
        }

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

            _recordingHandler.StopRecording(id, variables: variables, saveRecording: save);
        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequestAsync(id, Request, Response, RedirectableClient, RedirectlessClient);
        }
    }
}
