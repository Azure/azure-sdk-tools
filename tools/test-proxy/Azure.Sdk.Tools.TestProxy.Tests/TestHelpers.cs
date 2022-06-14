using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public static class TestHelpers
    {
        public static Stream GenerateStreamRequestBody(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static ModifiableRecordSession LoadRecordSession(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);

            return new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));
        }

        public static RecordingHandler LoadRecordSessionIntoInMemoryStore(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var guid = Guid.NewGuid().ToString();
            var session = new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));

            RecordingHandler handler = new RecordingHandler(Directory.GetCurrentDirectory());
            handler.InMemorySessions.TryAdd(guid, session);

            return handler;
        }

        public static string GenerateStringFromStream(Stream s)
        {
            s.Position = 0;
            using StreamReader reader = new StreamReader(s);

            return reader.ReadToEnd();
        }

        public static byte[] GenerateByteRequestBody(string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        public static HttpRequest CreateRequestFromEntry(RecordEntry entry)
        {
            var context = new DefaultHttpContext();
            if(entry.Request.Body != null)
            {
                context.Request.Body = new BinaryData(entry.Request.Body).ToStream();
            }
            context.Request.Method = entry.RequestMethod.ToString();
            foreach (var header in entry.Request.Headers)
            {
                context.Request.Headers[header.Key] = header.Value;
            }

            context.Request.Headers["x-recording-upstream-base-uri"] = entry.RequestUri;

            var uri = new Uri(entry.RequestUri);
            context.Request.Host = new HostString(uri.Authority);
            context.Request.QueryString = new QueryString(uri.Query);
            context.Request.Path = uri.AbsolutePath;
            context.Features.Get<IHttpRequestFeature>().RawTarget = context.Request.Path + context.Request.QueryString;
            return context.Request;
        }
    }
}
