// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly RecordingHandler _recordingHandler;

        public Audit(RecordingHandler recordingHandler)
        {
            _recordingHandler = recordingHandler;
        }

        [HttpGet]
        public async Task Logs()
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
    }
}
