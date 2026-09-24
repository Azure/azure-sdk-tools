using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using System.Diagnostics;

namespace APIViewWeb.MiddleWare
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly TelemetryClient _telemetryClient;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, TelemetryClient telemetryClient)
        {
            _next = next;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public async Task Invoke(HttpContext context)
        {
            context.Response.Headers["x-operation-id"] = Activity.Current?.TraceId.ToString();
            var requestTitle = $"{context.Request.Method} {context.Request.Path}";
            var requestInfo = new Dictionary<string, object>();

            var requestTelemetry = new RequestTelemetry
            {
                Name = requestTitle,
                Url = context.Request.GetUri(),
                Timestamp = DateTimeOffset.UtcNow
            };

            if (context.Request.Headers.ContainsKey("x-correlation-id"))
            {
                var correlationId = context.Request.Headers["x-correlation-id"].ToString();
                requestTelemetry.Properties.Add("CorrelationId", correlationId);
                requestInfo.Add("CorrelationId", correlationId);
            }

            var requestQueryParams = new Dictionary<string, string>();
            foreach (var query in context.Request.Query)
            {
                requestQueryParams.Add(query.Key, query.Value);
            }
            var requestQueryParamsString = JsonSerializer.Serialize(requestQueryParams);
            requestInfo.Add("Query Parameters", requestQueryParamsString);
            requestTelemetry.Properties.Add("Query Parameters", requestQueryParamsString);

            var requestRouteParams = new Dictionary<string, object>();
            foreach (var route in context.Request.RouteValues)
            {
                requestRouteParams.Add(route.Key, route.Value);
            }
            var requestRouteParamsString = JsonSerializer.Serialize(requestRouteParams);
            requestInfo.Add("Route Parameters", requestRouteParamsString);
            requestTelemetry.Properties.Add("Route Parameters", requestRouteParamsString);

            if (context.Request.ContentLength > 0 && !IsMultipartFormData(context))
            {
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;

                var sanitizedBody = MaskSensitiveData(body);
                if (!String.IsNullOrEmpty(sanitizedBody))
                {
                    requestInfo.Add("Request Body", sanitizedBody);
                    requestTelemetry.Properties.Add("Request Body", sanitizedBody);
                }
            }


            var operation = _telemetryClient.StartOperation(requestTelemetry);

            using (_logger.BeginScope(requestInfo))
            {
                _logger.LogInformation($"Incoming Request: {requestTitle}");
                try
                {
                    await _next(context);
                    requestTelemetry.ResponseCode = context.Response.StatusCode.ToString();
                    requestTelemetry.Success = context.Response.StatusCode < 400;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"An unhandled exception occurred.");
                    _telemetryClient.TrackException(ex);
                    requestTelemetry.Success = false;
                    throw;
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                    _logger.LogInformation($"Response Status Code: {context.Response.StatusCode}");
                }
            }
            
        }

        private bool IsMultipartFormData(HttpContext context)
        {
            return context.Request.ContentType != null &&
                   context.Request.ContentType.Contains("multipart/form-data");
        }

        private string MaskSensitiveData(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return body;

            try
            {
                var jsonDocument = JsonDocument.Parse(body);
                var rootElement = jsonDocument.RootElement;

                var sanitizedData = new Dictionary<string, object>();

                foreach (var property in rootElement.EnumerateObject())
                {
                    if (property.Name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("key", StringComparison.OrdinalIgnoreCase))
                    {
                        sanitizedData[property.Name] = "****";
                    }
                    else if (property.Name.Equals("fileContent", StringComparison.OrdinalIgnoreCase))
                    {
                        sanitizedData[property.Name] = "[File content excluded]";
                    }
                    else
                    {
                        sanitizedData[property.Name] = property.Value.GetRawText();
                    }
                }

                return JsonSerializer.Serialize(sanitizedData);
            }
            catch
            {
                return String.Empty;
            }
        }
    }
}
