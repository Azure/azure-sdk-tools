using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

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

        public static byte[] GenerateByteRequestBody(string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }
    }
}
