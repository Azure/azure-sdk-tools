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
    public sealed class Admin : ControllerBase
    {
        private readonly RecordingHandler _recordingHandler;

        public Admin(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;

        [HttpPost]
        public void StartSession()
        {
            // to begin with, recordings should still be located in their home repository
            if (Request.Headers.TryGetValue("x-recording-sha", out var sha))
            {
                _recordingHandler.Checkout(sha);
            }
        }

        [HttpPost]
        public void StopSession()
        {
            // so far, nothing necessary here
        }


        [HttpPost]
        public void AddTransform()
        {
            var tName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", true);
            ResponseTransform t = (ResponseTransform)GetTransform(tName);

            if (recordingId != null)
            {
                _recordingHandler.AddPlaybackTransform(recordingId, t);
            }
            else
            {
                _recordingHandler.Transforms.Add(t);
            }
        }

        [HttpPost]
        public void AddSanitizer()
        {
            var sName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", true);
            RecordedTestSanitizer s = (RecordedTestSanitizer)GetSanitizer(sName);

            if (recordingId != null)
            {
                _recordingHandler.AddRecordSanitizer(recordingId, s);
            }
            else
            {
                _recordingHandler.Sanitizers.Add(s);
            }
        }

        [HttpPost]
        public void SetMatcher()
        {
            var mName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", true);
            RecordMatcher m = (RecordMatcher)GetMatcher(mName);

            if (recordingId != null)
            {
                _recordingHandler.SetPlaybackMatcher(recordingId, m);
            }
            else
            {
                _recordingHandler.Matcher = m; 
            }
        }

        public object GetSanitizer(string name)
        {
            try
            {
                Type t = Type.GetType(name);
                return Activator.CreateInstance(t);
            }
            catch
            {
                throw new Exception(String.Format("Sanitizer named {0} is not recognized", name));
            }
        }

        public object GetTransform(string name)
        {
            try
            {
                Type t = Type.GetType(name);
                return Activator.CreateInstance(t);
            }
            catch
            {
                throw new Exception(String.Format("Transform named {0} is not recognized", name));
            }
        }

        public object GetMatcher(string name)
        {
            try
            {
                Type t = Type.GetType(name);
                return Activator.CreateInstance(t);
            }
            catch
            {
                throw new Exception(String.Format("Matcher named {0} is not recognized", name));
            }
        }
    }
}
