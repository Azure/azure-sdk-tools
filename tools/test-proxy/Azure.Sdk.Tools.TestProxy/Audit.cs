// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Audit : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly RecordingHandler _recordingHandler;

        public Audit(RecordingHandler recordingHandler, ILoggerFactory loggerFactory)
        {
            _recordingHandler = recordingHandler;
            _logger = loggerFactory.CreateLogger<Record>();
        }

        [HttpGet]
        public async Task GetAuditLogs()
        {

            var allAuditSessions = _recordingHandler.RetrieveOngoingAuditLogs();
            allAuditSessions.AddRange(_recordingHandler.AuditSessions.Values);

            StringBuilder stringBuilder = new StringBuilder();

            foreach (var auditLogQueue in allAuditSessions) {
                while (auditLogQueue.TryDequeue(out var logItem))
                {
                    stringBuilder.Append(logItem.ToCsvString() + Environment.NewLine);
                }
            }

            Response.ContentType = "text/plain";

            await Response.WriteAsync(stringBuilder.ToString());
        }


        [HttpPost]
        public async Task Push([FromBody()] IDictionary<string, object> options = null)
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);

            var pathToAssets = RecordingHandler.GetAssetsJsonLocation(StoreResolver.ParseAssetsJsonBody(options), _recordingHandler.ContextDirectory);

            await _recordingHandler.Store.Push(pathToAssets);
        }

        [HttpPost]
        [AllowEmptyBody]
        public async Task Stop([FromBody()] IDictionary<string, string> variables = null)
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

            DebugLogger.LogAdminRequestDetails(_logger, Request);

            await _recordingHandler.StopRecording(id, variables: variables, saveRecording: save);
        }

        public async Task HandleRequest()
        {
            string id = RecordingHandler.GetHeader(Request, "x-recording-id");

            await _recordingHandler.HandleRecordRequestAsync(id, Request, Response);
        }
    }
}
