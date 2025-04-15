using APIViewWeb;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using APIViewWeb.MiddleWare;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http;
using System.Text;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Linq;
using System.Collections.Generic;
using System;

namespace APIViewUnitTests
{
    public class RequestLoggingMiddlewareTests
    {
        [Fact]
        public async Task RequestLoggingMiddleware_Logs_Request_And_Masks_Sensitive_Data()
        {
            var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
            var capturedScopes = new List<IDictionary<string, object>>();
            mockLogger
                .Setup(logger => logger.BeginScope(It.IsAny<IDictionary<string, object>>()))
                .Callback((IDictionary<string, object> scope) =>
                {
                    capturedScopes.Add(scope);
                })
                .Returns(Mock.Of<IDisposable>());

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddLogging();
            builder.Services.AddSingleton(mockLogger.Object);
            builder.Services.AddApplicationInsightsTelemetry();
            builder.WebHost.UseTestServer();

            var app = builder.Build();
            app.UseMiddleware<RequestLoggingMiddleware>();

            app.Map("/test", async context =>
            {
                await context.Response.WriteAsync("Hello, World!");
            });

            await app.StartAsync();
            var client = app.GetTestClient();

            var requestBody = "{\"username\":\"testuser\",\"password\":\"12345\",\"token\":\"abcdef\"}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/test?query1=One&query2=Two", content);

            var logMessages = mockLogger.Invocations
                .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
                .Select(invocation =>
                {
                    var logLevel = (LogLevel)invocation.Arguments[0];
                    var state = invocation.Arguments[2] as IReadOnlyList<KeyValuePair<string, object>>;
                    return state?.FirstOrDefault(kv => kv.Key == "{OriginalFormat}").Value?.ToString();
                })
                .ToList();

            Assert.NotEmpty(capturedScopes);

            Assert.Contains("Incoming Request: POST /test", logMessages);
            Assert.Contains("Response Status Code: 200", logMessages);
        }
    }
}
