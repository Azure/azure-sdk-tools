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

                if (e is TestRecordingMismatchException)
                {
                    response.Headers.Append("x-request-mismatch", "true");

                    // grab the exception up till Remaining Entries: which can potentially push the header size past the default limit of ~8k
                    var onlyMisMatchMessage = e.Message.Split("Remaining Entries:")[0];
                    response.Headers.Append("x-request-mismatch-error", Convert.ToBase64String(Encoding.UTF8.GetBytes(onlyMisMatchMessage)));
                }
                else
                {
                    response.Headers.Append("x-request-known-exception", "true");
                    response.Headers.Append("x-request-known-exception-error", Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Message)));
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

                response.Headers.Append("x-request-exception", "true");
                response.Headers.Append("x-request-exception-error", Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Message)));

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
