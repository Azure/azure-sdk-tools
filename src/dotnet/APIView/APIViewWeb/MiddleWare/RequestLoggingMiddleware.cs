using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using System.Linq;
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
            var traceId = Activity.Current?.TraceId.ToString();
            var requestTelemetry = new RequestTelemetry
            {
                Name = $"{context.Request.Method} {context.Request.Path}",
                Url = context.Request.GetUri(),
                Timestamp = DateTimeOffset.UtcNow
            };

            var correlationId = context.Request.Headers["x-correlation-id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            context.Response.Headers["x-correlation-id"] = correlationId;
            context.Items["CorrelationId"] = correlationId;

            requestTelemetry.Properties["CorrelationId"] = correlationId;
            var operation = _telemetryClient.StartOperation(requestTelemetry);

            using (_logger.BeginScope(new Dictionary<string, object> { { "CorrelationId", correlationId } }))
            {
                _logger.LogInformation($"Incoming Request: {context.Request.Method} {context.Request.Path} {context.Request.QueryString}");

                foreach (var query in context.Request.Query)
                {
                    _logger.LogInformation($"{query.Key} = {query.Value}");
                }

                foreach (var route in context.Request.RouteValues)
                {
                    _logger.LogInformation($"{route.Key} = {route.Value}");
                }

                if (context.Request.ContentLength > 0 && !IsMultipartFormData(context))
                {
                    context.Request.EnableBuffering();
                    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    var sanitizedBody = MaskSensitiveData(body);
                    if (!String.IsNullOrEmpty(sanitizedBody))
                    {
                        _logger.LogInformation($"Request Body: {sanitizedBody}");
                    }
                }
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
