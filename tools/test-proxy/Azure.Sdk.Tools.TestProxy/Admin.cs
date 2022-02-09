// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
        public async Task Reset()
        {
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);
            var recordingId = RecordingHandler.GetHeader(Request, "x-recording-id", allowNulls: true);

            _recordingHandler.SetDefaultExtensions(recordingId);
        }

        [HttpGet]
        public async Task IsAlive()
        {
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);
            Response.StatusCode = 200;
        }

        [HttpPost]
        public async Task AddTransform()
        {
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);
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
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);
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
        public async Task SetMatcher()
        {
            await DebugLogger.LogRequestDetailsAsync(_logger, Request);
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
                            throw new HttpException(HttpStatusCode.BadRequest, $"Parameter {param.Name} was passed with no value. Please check the request body and try again.");
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
                        throw new HttpException(HttpStatusCode.BadRequest, $"Required parameter key {param} was not found in the request body.");
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
