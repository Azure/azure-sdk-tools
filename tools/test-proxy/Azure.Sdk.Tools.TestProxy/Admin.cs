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
using System.Text;
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


        [HttpGet]
        public void IsAlive(){
            Response.StatusCode = 200;
        }


        [HttpPost]
        public void AddTransform()
        {
            var tName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            ResponseTransform t = (ResponseTransform)GetTransform(tName, GetBody(Request));

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
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordedTestSanitizer s = (RecordedTestSanitizer)GetSanitizer(sName, GetBody(Request));

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
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);
            
            RecordMatcher m = (RecordMatcher)GetMatcher(mName, GetBody(Request));

            if (recordingId != null)
            {
                _recordingHandler.SetPlaybackMatcher(recordingId, m);
            }
            else
            {
                _recordingHandler.Matcher = m; 
            }
        }

        public object GetSanitizer(string name, JsonDocument body)
        {
            return GenerateInstance("Azure.Sdk.Tools.TestProxy.Sanitizers.", name, body);
        }

        public object GetTransform(string name, JsonDocument body)
        {
            return GenerateInstance("Azure.Sdk.Tools.TestProxy.Transforms.", name, body);
        }

        public object GetMatcher(string name, JsonDocument body)
        {
            return GenerateInstance("Azure.Sdk.Tools.TestProxy.Matchers.", name, body);
        }

        public object GenerateInstance(string typePrefix, string name, JsonDocument body = null)
        {
            try
            {
                Type t = Type.GetType(typePrefix + name);

                if (body != null)
                {
                    var arg_list = new List<Object> { };

                    // we are deliberately assuming here that there will only be a single constructor
                    var ctor = t.GetConstructors()[0];
                    var paramsSet = ctor.GetParameters().Select(x => x.Name);

                    // walk across our constructor params. check inside the body for a resulting value for each of them
                    foreach (var param in paramsSet)
                    {
                        if (body.RootElement.TryGetProperty(param, out var jsonElement)){

                            var valueResult = jsonElement.GetString();
                            arg_list.Add((object)valueResult);
                        }
                        else
                        {
                            // TODO: make this a specific type of exception
                            throw new Exception(String.Format("Required parameter key {0} was not found in the request body.", param));
                        }
                    }

                    return Activator.CreateInstance(t, arg_list.ToArray());
                }
                else
                {
                    return Activator.CreateInstance(t);
                }
            }
            catch
            {
                throw new Exception(String.Format("Requested type {0} is not not recognized.", typePrefix + name));
            }
        }


        private static JsonDocument GetBody(HttpRequest req)
        {
            if (req.Body.Length > 0)
            {
                return JsonDocument.Parse(req.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
            }

            return null;
        }
    }
}
