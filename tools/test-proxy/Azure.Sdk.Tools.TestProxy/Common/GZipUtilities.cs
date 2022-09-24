using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// Utility methods to compress and decompress content to/from GZip.
    /// </summary>
    public static class GZipUtilities
    {
        private const string Gzip = "gzip";
        private const string ContentEncoding = "Content-Encoding";

        public static byte[] CompressBody(byte[] incomingBody, IDictionary<string, string[]> headers)
        {
            if (headers.TryGetValue(ContentEncoding, out var values) && values.Contains(Gzip))
            {
                return CompressBodyCore(incomingBody);
            }

            return incomingBody;
        }

        public static byte[] CompressBody(byte[] incomingBody, IHeaderDictionary headers)
        {
            if (headers.TryGetValue(ContentEncoding, out var values) && values.Contains(Gzip))
            {
                return CompressBodyCore(incomingBody);
            }

            return incomingBody;
        }

        public static byte[] CompressBodyCore(byte[] body)
        {
            using var uncompressedStream = new MemoryStream(body);
            using var resultStream = new MemoryStream();
            using (var compressedStream = new GZipStream(resultStream, CompressionMode.Compress))
            {
                uncompressedStream.CopyTo(compressedStream);
            }
            return resultStream.ToArray();
        }

        public static byte[] DecompressBody(MemoryStream incomingBody, HttpContentHeaders headers)
        {
            if (headers.TryGetValues(ContentEncoding, out var values) && values.Contains(Gzip))
            {
                return DecompressBodyCore(incomingBody);
            }

            return incomingBody.ToArray();
        }

        public static byte[] DecompressBody(byte[] incomingBody, IHeaderDictionary headers)
        {
            if (headers.TryGetValue(ContentEncoding, out var values) && values.Contains(Gzip))
            {
                return DecompressBodyCore(new MemoryStream(incomingBody));
            }

            return incomingBody;
        }

        private static byte[] DecompressBodyCore(MemoryStream stream)
        {
            using var uncompressedStream = new GZipStream(stream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            uncompressedStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }
}