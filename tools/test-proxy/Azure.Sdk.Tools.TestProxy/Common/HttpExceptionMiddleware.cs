using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class HttpExceptionMiddleware
    {
        private readonly RequestDelegate next;

        public HttpExceptionMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await this.next.Invoke(context);
            }
            catch (HttpException e)
            {
                var response = context.Response;
                if (response.HasStarted)
                {
                    throw;
                }

                int statusCode = (int)e.StatusCode;
                if (statusCode >= 500 && statusCode <= 599)
                {
                    throw;
                }

                response.Clear();
                response.StatusCode = statusCode;
                response.ContentType = "application/json;";

                var encodedException = Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Message));

                if (e is TestRecordingMismatchException)
                {
                    response.Headers.Add("x-request-mismatch", "true");
                    response.Headers.Add("x-request-mismatch-error", encodedException);
                }
                else
                {
                    response.Headers.Add("x-request-known-exception", "true");
                    response.Headers.Add("x-request-known-exception-error", encodedException);
                }
                
                var bodyObj = new
                {
                    Message = e.Message,
                    Status = e.StatusCode.ToString()
                };

                DebugLogger.LogError(e.Message);

                var body = JsonSerializer.Serialize(bodyObj);
                await context.Response.WriteAsync(body);
            }
            catch (Exception e)
            {
                var response = context.Response;
                int unexpectedStatusCode = 500;

                response.Clear();
                response.StatusCode = unexpectedStatusCode;
                response.ContentType = "application/json";

                response.Headers.Add("x-request-exception", "true");
                response.Headers.Add("x-request-exception-error", Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Message)));

                DebugLogger.LogError(unexpectedStatusCode, e);

                var bodyObj = new
                {
                    Message = e.Message,
                    Status = unexpectedStatusCode.ToString(),
                    StackTrace = e.StackTrace,
                };

                var body = JsonSerializer.Serialize(bodyObj);
                await context.Response.WriteAsync(body);
            }
        }
    }
}
