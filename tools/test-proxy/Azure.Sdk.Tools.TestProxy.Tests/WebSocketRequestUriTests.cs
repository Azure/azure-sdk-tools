using Azure.Sdk.Tools.TestProxy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class WebSocketRequestUriTests
    {
        [Fact]
        public void GetWebSocketRequestUri_UsesUpstreamBaseUri()
        {
            var originalMode = Startup.ProxyConfiguration.Mode;
            try
            {
                Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
                var context = new DefaultHttpContext();
                context.Request.Headers["x-recording-upstream-base-uri"] = "https://example.com";
                context.Features.Get<IHttpRequestFeature>().RawTarget = "/socket?x=1";

                var uri = RecordingHandler.GetWebSocketRequestUri(context.Request);

                Assert.Equal("wss", uri.Scheme);
                Assert.Equal("example.com", uri.Host);
                Assert.Equal("/socket?x=1", uri.PathAndQuery);
            }
            finally
            {
                Startup.ProxyConfiguration.Mode = originalMode;
            }
        }

        [Fact]
        public void GetWebSocketRequestUri_MapsHttpsToWss()
        {
            var originalMode = Startup.ProxyConfiguration.Mode;
            try
            {
                Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
                var context = new DefaultHttpContext();
                context.Request.Scheme = "https";
                context.Request.Host = new HostString("example.com", 443);
                context.Request.Path = "/socket";
                context.Request.QueryString = new QueryString("?a=b");
                context.Features.Get<IHttpRequestFeature>().RawTarget = "/socket?a=b";

                var uri = RecordingHandler.GetWebSocketRequestUri(context.Request);

                Assert.Equal("wss", uri.Scheme);
                Assert.Equal("example.com", uri.Host);
                Assert.Equal("/socket?a=b", uri.PathAndQuery);
            }
            finally
            {
                Startup.ProxyConfiguration.Mode = originalMode;
            }
        }

        [Fact]
        public void GetWebSocketRequestUri_MapsHttpToWs()
        {
            var originalMode = Startup.ProxyConfiguration.Mode;
            try
            {
                Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
                var context = new DefaultHttpContext();
                context.Request.Scheme = "http";
                context.Request.Host = new HostString("example.com", 80);
                context.Request.Path = "/socket";
                context.Request.QueryString = new QueryString("?a=b");
                context.Features.Get<IHttpRequestFeature>().RawTarget = "/socket?a=b";

                var uri = RecordingHandler.GetWebSocketRequestUri(context.Request);

                Assert.Equal("ws", uri.Scheme);
                Assert.Equal("example.com", uri.Host);
                Assert.Equal("/socket?a=b", uri.PathAndQuery);
            }
            finally
            {
                Startup.ProxyConfiguration.Mode = originalMode;
            }
        }
    }
}
