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

    }
}
