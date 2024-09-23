using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Core;

namespace Azure.Sdk.Tools.TestProxy.HttpPipelineSample
{
    /// <summary>
    /// This is a custom transport implementation that will automatically redirect requests to the TestProxy.
    /// 
    /// Test implementors will pass this transport to the class they are testing, so that their recordings can work.
    /// </summary>
    public class TestProxyTransport : HttpPipelineTransport
    {
        private readonly HttpPipelineTransport _transport;
        private readonly string _host;
        private readonly int? _port;
        private readonly string _recordingId;
        private readonly string _mode;

        public TestProxyTransport(HttpPipelineTransport transport, string host, int? port, string recordingId, string mode)
        {
            _transport = transport;
            _host = host;
            _port = port;
            _recordingId = recordingId;
            _mode = mode;
        }

        public override Request CreateRequest()
        {
            return _transport.CreateRequest();
        }

        public override void Process(HttpMessage message)
        {
            RedirectToTestProxy(message);
            _transport.Process(message);
        }

        public override ValueTask ProcessAsync(HttpMessage message)
        {
            RedirectToTestProxy(message);
            return _transport.ProcessAsync(message);
        }

        private void RedirectToTestProxy(HttpMessage message)
        {
            message.Request.Headers.Add("x-recording-id", _recordingId);
            message.Request.Headers.Add("x-recording-mode", _mode);

            var baseUri = new RequestUriBuilder()
            {
                Scheme = message.Request.Uri.Scheme,
                Host = message.Request.Uri.Host,
                Port = message.Request.Uri.Port,
            };
            message.Request.Headers.Add("x-recording-upstream-base-uri", baseUri.ToString());

            message.Request.Uri.Host = _host;
            if (_port.HasValue)
            {
                message.Request.Uri.Port = _port.Value;
            }
        }
    }
}
