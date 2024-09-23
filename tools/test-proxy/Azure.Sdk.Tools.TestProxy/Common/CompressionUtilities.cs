using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// Utility methods to compress and decompress content to/from GZip.
    /// </summary>
    public static class CompressionUtilities
    {
        private const string Gzip = "gzip";
        private const string Brotli = "br";

        private const string ContentEncoding = "Content-Encoding";

        public static byte[] CompressBody(byte[] incomingBody, IDictionary<string, string[]> headers)
        {
            if (headers.TryGetValue(ContentEncoding, out var values) && (values.Contains(Gzip) || values.Contains(Brotli)))
            {
                return CompressBodyCore(incomingBody, values);
            }

            return incomingBody;
        }

        public static byte[] CompressBody(byte[] incomingBody, IHeaderDictionary headers)
        {
            if (headers.TryGetValue(ContentEncoding, out var values) && (values.Contains(Gzip) || values.Contains(Brotli)))
            {
                return CompressBodyCore(incomingBody, values);
            }

            return incomingBody;
        }

        public static byte[] CompressBodyCore(byte[] body, StringValues encodingValues)
        {
            using var uncompressedStream = new MemoryStream(body);
            using var resultStream = new MemoryStream();

            if (encodingValues.Contains(Brotli))
            {
                using (var compressedStream = new BrotliStream(resultStream, CompressionMode.Compress))
                {
                    uncompressedStream.CopyTo(compressedStream);
                }
                return resultStream.ToArray();
            }
            
            if (encodingValues.Contains(Gzip))
            {
                using (var compressedStream = new GZipStream(resultStream, CompressionMode.Compress))
                {
                    uncompressedStream.CopyTo(compressedStream);
                }
                return resultStream.ToArray();
            }
            
            return body;
        }

        public static byte[] DecompressBody(MemoryStream incomingBody, HttpContentHeaders headers)
        {
            if (headers.TryGetValues(ContentEncoding, out var values) && (values.Contains(Gzip) || values.Contains(Brotli)))
            {
                return DecompressBodyCore(incomingBody, values.ToArray());
            }

            return incomingBody.ToArray();
        }

        public static byte[] DecompressBody(byte[] incomingBody, IHeaderDictionary headers)
        {
            if (headers.TryGetValue(ContentEncoding, out var values) && (values.Contains(Gzip) || values.Contains(Brotli)))
            {
                return DecompressBodyCore(new MemoryStream(incomingBody), values);
            }

            return incomingBody;
        }

        private static byte[] DecompressBodyCore(MemoryStream stream, StringValues encodingValues)
        {
            Stream uncompressedStream = null;

            if (encodingValues.Contains(Brotli))
            {
                uncompressedStream = new BrotliStream(stream, CompressionMode.Decompress);
            }

            if (encodingValues.Contains(Gzip))
            {
                uncompressedStream = new GZipStream(stream, CompressionMode.Decompress);
            }
            
            if (uncompressedStream == null)
            {
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"The test-proxy does not currently support decompression for content encoded with \"{encodingValues.ToString()}.\". "
                    + "Please file an issue against Azure/azure-sdk-tools with the context of your request.");
            }

            using var resultStream = new MemoryStream();
            uncompressedStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }
}
