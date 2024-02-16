using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Azure.Sdk.Tools.HttpFaultInjector
{
    public class UpstreamResponse : IDisposable
    {
        private readonly HttpContent _content = null;
        private readonly Stream _contentStream = null;
        public UpstreamResponse(HttpContent content)
        {
            _content = content;
            ContentLength = _content.Headers.ContentLength ?? throw new ArgumentNullException("ContentLength must not be null.");
        }

        public UpstreamResponse(MemoryStream contentStream)
        {
            _contentStream = contentStream;
            ContentLength = contentStream.Length;
        }

        public int StatusCode { get; set; }
        public IEnumerable<KeyValuePair<string, StringValues>> Headers { get; set; }
        public async Task<Stream> GetContentStreamAsync(CancellationToken cancellationToken)
        {
            return _contentStream != null ? _contentStream : await _content.ReadAsStreamAsync(cancellationToken);
        }

        public long ContentLength { get; }

        public void Dispose()
        {
            _content?.Dispose();
            _contentStream?.Dispose();
        }
    }
}
