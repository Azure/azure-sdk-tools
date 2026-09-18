using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class CompressionUtilityTests
    {
        [Fact]
        public void EnsureDecompressionPristineBytes()
        {
            // generate 
            byte[] uncompressedBody = Encoding.UTF8.GetBytes("\"{\\u0022TableName\\u0022:    \\u0022listtable09bf2a3d\\u0022}\"");
            byte[] compressedBody = CompressionUtilities.CompressBodyCore(uncompressedBody, new string[] { "gzip" });

            byte[] savedCompressedBody = new byte[compressedBody.Length];
            compressedBody.CopyTo(savedCompressedBody, 0);

            var headerDict = new HeaderDictionary();
            headerDict.Add("Content-Encoding", new string[1] { "gzip" });

            // intentionally testing DecompressBody vs DecompressBodyCore, as that is where the header values are intercepted and treated differently
            byte[] decompressedResult = CompressionUtilities.DecompressBody(compressedBody, headerDict);


            Assert.Equal(compressedBody, savedCompressedBody);
            Assert.NotEqual(decompressedResult, compressedBody);
        }

        [Theory]
        [InlineData("gzip")]
        [InlineData("br")]
        public void PlaybackCompressionCacheReusesUnchangedBodies(string encoding)
        {
            var response = new RequestOrResponse { Body = Encoding.UTF8.GetBytes("response body") };
            response.Headers.Add("Content-Encoding", new[] { encoding });

            var compressed = response.GetBodyForPlayback(cacheCompression: true);
            Assert.Same(compressed, response.GetBodyForPlayback(cacheCompression: true));
            response.Body = (byte[])response.Body.Clone();
            response.Headers["x-request-id"] = new[] { Guid.NewGuid().ToString() };
            Assert.Same(compressed, response.GetBodyForPlayback(cacheCompression: true));
            Assert.NotSame(compressed, response.GetBodyForPlayback(cacheCompression: false));

            var headers = new HeaderDictionary { ["Content-Encoding"] = encoding };
            Assert.Equal(response.Body, CompressionUtilities.DecompressBody(compressed, headers));
        }

        [Theory]
        [InlineData("gzip", "br")]
        [InlineData("br", "gzip")]
        public void PlaybackCompressionCacheTracksBodyAndEncodingChanges(string encoding, string nextEncoding)
        {
            var response = new RequestOrResponse { Body = Encoding.UTF8.GetBytes("response body") };
            response.Headers.Add("Content-Encoding", new[] { encoding });
            var original = response.GetBodyForPlayback(cacheCompression: true);

            response.Body[0] = (byte)'R';
            var changedBody = response.GetBodyForPlayback(cacheCompression: true);
            Assert.NotSame(original, changedBody);
            var headers = new HeaderDictionary { ["Content-Encoding"] = encoding };
            Assert.Equal(response.Body, CompressionUtilities.DecompressBody(changedBody, headers));

            response.Headers["Content-Encoding"][0] = nextEncoding;
            var changedEncoding = response.GetBodyForPlayback(cacheCompression: true);
            Assert.NotSame(changedBody, changedEncoding);
            headers["Content-Encoding"] = nextEncoding;
            Assert.Equal(response.Body, CompressionUtilities.DecompressBody(changedEncoding, headers));

            response.Headers.Remove("Content-Encoding");
            Assert.Same(response.Body, response.GetBodyForPlayback(cacheCompression: true));
            response.Headers["Content-Encoding"] = new[] { nextEncoding };
            Assert.NotSame(changedEncoding, response.GetBodyForPlayback(cacheCompression: true));
        }

        [Fact]
        public void UncompressedPlaybackKeepsOriginalBuffer()
        {
            var response = new RequestOrResponse { Body = Encoding.UTF8.GetBytes("response body") };
            Assert.Same(response.Body, response.GetBodyForPlayback(cacheCompression: true));
            Assert.Same(response.Body, response.GetBodyForPlayback(cacheCompression: false));
        }

    }
}
