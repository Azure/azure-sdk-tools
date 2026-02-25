using System;
using System.IO;
using System.Net;
using System.Text;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.WebUtilities;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    public class ContentDispositionFilePathSanitizer : RecordedTestSanitizer
    {
        public ContentDispositionFilePathSanitizer()
        {
            _scope = SanitizerScope.Body;
        }

        public override void SanitizeBody(RequestOrResponse message)
        {
            if (message.Body == null || !ContentTypeUtilities.IsMultipart(message.Headers, out var boundary))
            {
                return;
            }

            message.Body = NormalizeContentDispositionHeaderFilePaths(boundary, message.Body);
        }

        private byte[] NormalizeContentDispositionHeaderFilePaths(string boundary, byte[] raw)
        {
            boundary = MultipartUtilities.ResolveFirstBoundary(boundary, raw);

            var reader = new MultipartReader(boundary, new MemoryStream(raw));
            using var outStream = new MemoryStream();

            byte[] boundaryStart = Encoding.ASCII.GetBytes($"--{boundary}\r\n");
            byte[] boundaryClose = Encoding.ASCII.GetBytes($"--{boundary}--\r\n");

            try
            {
                MultipartSection section;
                while ((section = reader.ReadNextSectionAsync()
                                         .GetAwaiter()
                                         .GetResult()) != null)
                {
                    byte[] original = MultipartUtilities.ReadAllBytes(section.Body);
                    byte[] newBody;

                    if (MultipartUtilities.IsNestedMultipart(section.Headers, out var childBoundary))
                    {
                        newBody = NormalizeContentDispositionHeaderFilePaths(childBoundary, original);
                    }
                    else
                    {
                        newBody = original;
                    }

                    outStream.Write(boundaryStart);
                    foreach (var h in section.Headers)
                    {
                        var newValue = h.Value;
                        if (h.Key == "Content-Disposition")
                        {
                            newValue = MultipartUtilities.NormalizeFilenameFromContentDispositionValue(h.Value);
                        }
                        var headerLine = $"{h.Key}: {newValue}\r\n";
                        outStream.Write(Encoding.ASCII.GetBytes(headerLine));
                    }
                    outStream.Write(MultipartUtilities.CrLf);
                    outStream.Write(newBody);
                    outStream.Write(MultipartUtilities.CrLf);
                }
            }
            catch (IOException ex)
            {
                var byteContent = Convert.ToBase64String(raw);
                string message = $$"""
The test-proxy is unexpectedly unable to read this section of the config during sanitization: \"{{ex.Message}}\"
File an issue on Azure/azure-sdk-tools and include this base64 string for reproducibility:
{{byteContent}}
""";

                throw new HttpException(HttpStatusCode.InternalServerError, message);
            }

            outStream.Write(boundaryClose);
            return outStream.ToArray();
        }
    }
}
