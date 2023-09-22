// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger _logger;

        public Admin(RecordingHandler recordingHandler, ILoggerFactory loggingFactory)
        {
            _recordingHandler = recordingHandler;
            _logger = loggingFactory.CreateLogger<Admin>();
        }

        [HttpPost]
        public void Reset()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            _recordingHandler.SetDefaultExtensions(recordingId);
        }

        [HttpGet]
        public void IsAlive()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            Response.StatusCode = 200;
        }

        [HttpPost]
        public async Task AddTransform()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            var tName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            ResponseTransform t = (ResponseTransform)GetTransform(tName, await HttpRequestInteractions.GetBody(Request));

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
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            var sName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordedTestSanitizer s = (RecordedTestSanitizer)GetSanitizer(sName, await HttpRequestInteractions.GetBody(Request));

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
        public async Task AddSanitizers()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            // parse all of them first, any exceptions should pop here
            var workload = (await HttpRequestInteractions.GetBody<List<SanitizerBody>>(Request)).Select(s => (RecordedTestSanitizer)GetSanitizer(s.Name, s.Body)).ToList();

            if (workload.Count == 0)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "When bulk adding sanitizers, ensure there is at least one sanitizer added in each batch. Received 0 work items.");
            }

            // register them all
            foreach(var sanitizer in workload)
            {
                if (recordingId != null)
                {
                    _recordingHandler.AddSanitizerToRecording(recordingId, sanitizer);
                }
                else
                {
                    _recordingHandler.Sanitizers.Add(sanitizer);
                }
            }

        }


        [HttpPost]
        public async Task SetMatcher()
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);
            var mName = RecordingHandler.GetHeader(Request, "x-abstraction-identifier");
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            RecordMatcher m = (RecordMatcher)GetMatcher(mName, await HttpRequestInteractions.GetBody(Request));

            if (recordingId != null)
            {
                _recordingHandler.SetMatcherForRecording(recordingId, m);
            }
            else
            {
                _recordingHandler.Matcher = m;
            }
        }

        [HttpPost]
        [AllowEmptyBody]
        public void SetRecordingOptions([FromBody()] IDictionary<string, object> options = null)
        {
            DebugLogger.LogAdminRequestDetails(_logger, Request);

            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            _recordingHandler.SetRecordingOptions(options, recordingId);
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
            Type t = Type.GetType(typePrefix + name);

            if (t == null)
            {
                throw new HttpException(HttpStatusCode.BadRequest, String.Format("Requested type {0} is not not recognized.", typePrefix + name));
            }

            var arg_list = new List<Object> { };

            // we are deliberately assuming here that there will only be a single constructor
            var ctor = t.GetConstructors()[0];
            var paramsSet = ctor.GetParameters();

            // walk across our constructor params. check inside the body for a resulting value for each of them
            foreach (var param in paramsSet)
            {
                if (documentBody != null && documentBody.RootElement.TryGetProperty(param.Name, out var jsonElement))
                {
                    if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                    {
                        _logger.LogDebug("Request Body Content" + JsonSerializer.Serialize(documentBody.RootElement));
                    }

                    object argumentValue = null;
                    switch (jsonElement.ValueKind)
                    {
                        case JsonValueKind.Null:
                        case JsonValueKind.String:
                            argumentValue = jsonElement.GetString();
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            argumentValue = jsonElement.GetBoolean();
                            break;
                        case JsonValueKind.Object:
                            try
                            {
                                argumentValue = Activator.CreateInstance(param.ParameterType, new List<object> { jsonElement }.ToArray());
                            }
                            catch (Exception e)
                            {
                                if (e.InnerException is HttpException)
                                {
                                    throw e.InnerException;
                                }
                                else throw;
                            }
                            break;
                        default:
                            throw new HttpException(HttpStatusCode.BadRequest, $"{jsonElement.ValueKind} parameters are not supported");
                    }

                    if(argumentValue == null || (argumentValue is string stringResult && string.IsNullOrEmpty(stringResult)))
                    {
                        if (!acceptableEmptyArgs.Contains(param.Name))
                        {
                            throw new HttpException(HttpStatusCode.BadRequest, $"Parameter \"{param.Name}\" was passed with no value. Please check the request body and try again.");
                        }
                    }
                        
                    arg_list.Add((object)argumentValue);
                }
                else
                {
                    if (param.IsOptional)
                    {
                        arg_list.Add(param.DefaultValue);
                    }
                    else
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, $"Required parameter key \"{param.Name}\" was not found in the request body.");
                    }
                }
            }

            try
            {
                return Activator.CreateInstance(t, arg_list.ToArray());
            }
            catch(Exception e)
            {
                if (e.InnerException is HttpException)
                {
                    throw e.InnerException;
                }
                else throw;
            }
        }
    }
}
