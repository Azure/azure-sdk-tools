// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        [HttpPost]
        public void Reset()
        {
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            _recordingHandler.SetDefaultExtensions(recordingId);
        }

        [HttpGet]
        public void IsAlive()
        {
            Response.StatusCode = 200;
        }

        [HttpPost]
        public async Task AddTransform()
        {
            var tName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            ResponseTransform t = (ResponseTransform)GetTransform(tName, await GetBody(Request));

            if (recordingId != null)
            {
                _recordingHandler.AddTransformToRecording(recordingId, t);
            }
            else
            {
                _recordingHandler.Transforms.Add(t);
            }
        }

        [HttpPost]
        public async Task AddSanitizer()
        {
            var sName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordedTestSanitizer s = (RecordedTestSanitizer)GetSanitizer(sName, await GetBody(Request));

            if (recordingId != null)
            {
                _recordingHandler.AddSanitizerToRecording(recordingId, s);
            }
            else
            {
                _recordingHandler.Sanitizers.Add(s);
            }
        }

        [HttpPost]
        public async Task SetMatcher()
        {
            var mName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordMatcher m = (RecordMatcher)GetMatcher(mName, await GetBody(Request));

            if (recordingId != null)
            {
                _recordingHandler.SetMatcherForRecording(recordingId, m);
            }
            else
            {
                _recordingHandler.Matcher = m;
            }
        }

        public object GetSanitizer(string name, JsonDocument body)
        {
            return GenerateInstance("Azure.Sdk.Tools.TestProxy.Sanitizers.", name, new HashSet<string>() { "value" }, documentBody: body);
        }

        public object GetTransform(string name, JsonDocument body)
        {
            return GenerateInstance("Azure.Sdk.Tools.TestProxy.Transforms.", name, new HashSet<string>() { }, documentBody: body);
        }

        public object GetMatcher(string name, JsonDocument body)
        {
            return GenerateInstance("Azure.Sdk.Tools.TestProxy.Matchers.", name, new HashSet<string>() { }, documentBody:body);
        }

        private object GenerateInstance(string typePrefix, string name, HashSet<string> acceptableEmptyArgs, JsonDocument documentBody = null)
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
                    if (documentBody != null && documentBody.RootElement.TryGetProperty(param.Name, out var jsonElement))
                    {
                        var valueResult = jsonElement.GetString();

                        if(string.IsNullOrEmpty(valueResult))
                        {
                            if (!acceptableEmptyArgs.Contains(param.Name))
                            {
                                throw new HttpException(HttpStatusCode.BadRequest, $"Parameter {param.Name} was passed with no value. Please check the request body and try again.");
                            }
                        }
                        
                        arg_list.Add((object)valueResult);
                    }
                    else
                    {
                        if (param.IsOptional)
                        {
                            arg_list.Add(param.DefaultValue);
                        }
                        else
                        {
                            throw new HttpException(HttpStatusCode.BadRequest, $"Required parameter key {param} was not found in the request body.");
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
