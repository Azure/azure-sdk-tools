// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json;
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
            // so far, nothing necessary here
        }

        [HttpPost]
        public void StopSession()
        {
            // so far, nothing necessary here
        }

        [HttpGet]
        public void Reset()
        {
            _recordingHandler.SetDefaultExtensions();
        }

        [HttpGet]
        public void IsAlive()
        {
            Response.StatusCode = 200;
        }

        [HttpPost]
        public async void AddTransform()
        {
            var tName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            ResponseTransform t = (ResponseTransform)GetTransform(tName, await GetBody(Request));

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
        public async void AddSanitizer()
        {
            var sName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordedTestSanitizer s = (RecordedTestSanitizer)GetSanitizer(sName, await GetBody(Request));

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
        public async void SetMatcher()
        {
            var mName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordMatcher m = (RecordMatcher)GetMatcher(mName, await GetBody(Request));

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


                var arg_list = new List<Object> { };

                // we are deliberately assuming here that there will only be a single constructor
                var ctor = t.GetConstructors()[0];
                var paramsSet = ctor.GetParameters();

                // walk across our constructor params. check inside the body for a resulting value for each of them
                foreach (var param in paramsSet)
                {
                    if (body != null && body.RootElement.TryGetProperty(param.Name, out var jsonElement))
                    {
                        var valueResult = jsonElement.GetString();
                        arg_list.Add((object)valueResult);
                    }
                    else
                    {
                        if (param.IsOptional)
                        {
                            arg_list.Add(null);
                        }
                        else
                        {
                            // TODO: make this a specific argument not found exception
                            throw new Exception(String.Format("Required parameter key {0} was not found in the request body.", param));
                        }
                    }
                }

                return Activator.CreateInstance(t, arg_list.ToArray());
            }
            catch(Exception e)
            {
                e.Data.Add("Attempted Type", String.Format("Requested type {0} is not not recognized.", typePrefix + name));

                throw;
            }
        }


        private async static Task<JsonDocument> GetBody(HttpRequest req)
        {
            if (req.ContentLength > 0)
            {
                var result = await JsonDocument.ParseAsync(req.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });

                return result;
            }

            return null;
        }
    }
}
