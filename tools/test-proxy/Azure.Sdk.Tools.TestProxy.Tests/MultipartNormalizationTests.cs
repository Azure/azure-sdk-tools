// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class MultipartNormalizationTests
    {
        [Fact]
        public void NormalizeMultipartBody_NormalizesForwardSlashesToBackslashes()
        {
            // Arrange - Create a multipart body with forward slashes (Linux-style)
            var multipartBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets/audio_french.wav""
Content-Type: audio/wav

[binary content]
--boundary123--";

            var expectedNormalizedBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets\audio_french.wav""
Content-Type: audio/wav

[binary content]
--boundary123--";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "multipart/form-data; boundary=boundary123" } }
                },
                Body = Encoding.UTF8.GetBytes(multipartBody)
            };

            // Act
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert
            var actualNormalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(expectedNormalizedBody, actualNormalizedBody);
        }

        [Fact]
        public void NormalizeMultipartBody_NormalizesUrlEncodedForwardSlashes()
        {
            // Arrange - Create a multipart body with URL-encoded forward slashes
            var multipartBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets/audio.wav""; filename*=utf-8''Assets%2Faudio.wav
Content-Type: audio/wav

[binary content]
--boundary123--";

            var expectedNormalizedBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets\audio.wav""; filename*=utf-8''Assets%5Caudio.wav
Content-Type: audio/wav

[binary content]
--boundary123--";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "multipart/form-data; boundary=boundary123" } }
                },
                Body = Encoding.UTF8.GetBytes(multipartBody)
            };

            // Act
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert
            var actualNormalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(expectedNormalizedBody, actualNormalizedBody);
        }

        [Fact]
        public void NormalizeMultipartBody_PreservesBackslashes()
        {
            // Arrange - Create a multipart body with backslashes (Windows-style)
            var multipartBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets\audio_french.wav""
Content-Type: audio/wav

[binary content]
--boundary123--";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "multipart/form-data; boundary=boundary123" } }
                },
                Body = Encoding.UTF8.GetBytes(multipartBody)
            };

            var originalBody = Encoding.UTF8.GetString(requestOrResponse.Body);

            // Act
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert - Should remain unchanged
            var actualNormalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(originalBody, actualNormalizedBody);
        }

        [Fact]
        public void NormalizeMultipartBody_HandlesNonMultipartContent()
        {
            // Arrange - Create a non-multipart request
            var bodyContent = "This is not a multipart body";
            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "application/json" } }
                },
                Body = Encoding.UTF8.GetBytes(bodyContent)
            };

            var originalBody = Encoding.UTF8.GetString(requestOrResponse.Body);

            // Act
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert - Should remain unchanged
            var actualBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(originalBody, actualBody);
        }

        [Fact]
        public void NormalizeMultipartBody_HandlesNullBody()
        {
            // Arrange - Create a request with null body
            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "multipart/form-data; boundary=boundary123" } }
                },
                Body = null
            };

            // Act - Should not throw
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert
            Assert.Null(requestOrResponse.Body);
        }

        [Fact]
        public void NormalizeMultipartBody_NormalizesCaseInsensitiveUrlEncoding()
        {
            // Arrange - Test both lowercase %2f and uppercase %2F
            var multipartBody = @"--boundary123
Content-Disposition: form-data; name=""file1""; filename*=utf-8''Assets%2Faudio.wav
Content-Disposition: form-data; name=""file2""; filename*=utf-8''Assets%2faudio.wav
--boundary123--";

            var expectedNormalizedBody = @"--boundary123
Content-Disposition: form-data; name=""file1""; filename*=utf-8''Assets%5Caudio.wav
Content-Disposition: form-data; name=""file2""; filename*=utf-8''Assets%5Caudio.wav
--boundary123--";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "multipart/form-data; boundary=boundary123" } }
                },
                Body = Encoding.UTF8.GetBytes(multipartBody)
            };

            // Act
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert
            var actualNormalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(expectedNormalizedBody, actualNormalizedBody);
        }

        [Fact]
        public void CrossPlatformCompatibility_LinuxRecordingPlaybackOnWindows()
        {
            // Simulate recording made on Linux with forward slashes
            var boundary = "batch_test";
            
            // This is what would be in the recording file (normalized to Windows format during recording)
            var recordedMultipartBody = $@"--{boundary}
Content-Disposition: form-data; name=""file""; filename=""Assets\audio.wav""
Content-Type: audio/wav

test content
--{boundary}--
";

            // This is what comes from a Windows request (Windows-style backslashes)
            var incomingRequestBody = $@"--{boundary}
Content-Disposition: form-data; name=""file""; filename=""Assets\audio.wav""
Content-Type: audio/wav

test content
--{boundary}--
";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { $"multipart/form-data; boundary={boundary}" } }
                },
                Body = Encoding.UTF8.GetBytes(incomingRequestBody)
            };

            // Act - Normalize the incoming request
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert - Should match the recorded body
            var normalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(recordedMultipartBody, normalizedBody);
        }

        [Fact]
        public void CrossPlatformCompatibility_WindowsRecordingPlaybackOnLinux()
        {
            // Simulate recording made on Windows (normalized to Windows format during recording)
            var boundary = "batch_test";
            
            // This is what would be in the recording file (Windows-style backslashes)
            var recordedMultipartBody = $@"--{boundary}
Content-Disposition: form-data; name=""file""; filename=""Assets\audio.wav""
Content-Type: audio/wav

test content
--{boundary}--
";

            // This is what comes from a Linux request (Linux-style forward slashes)
            var incomingRequestBody = $@"--{boundary}
Content-Disposition: form-data; name=""file""; filename=""Assets/audio.wav""
Content-Type: audio/wav

test content
--{boundary}--
";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { $"multipart/form-data; boundary={boundary}" } }
                },
                Body = Encoding.UTF8.GetBytes(incomingRequestBody)
            };

            // Act - Normalize the incoming request
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert - Should match the recorded body after normalization
            var normalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(recordedMultipartBody, normalizedBody);
        }

        [Fact]
        public void NormalizeMultipartBody_PreservesNonFilenameSlashes()
        {
            // Arrange - Create a multipart body with slashes in Content-Type which should NOT be normalized
            var multipartBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets/test.wav""
Content-Type: custom/type; param=""value/with/slashes""

[binary content]
--boundary123--";

            var expectedNormalizedBody = @"--boundary123
Content-Disposition: form-data; name=""file""; filename=""Assets\test.wav""
Content-Type: custom/type; param=""value/with/slashes""

[binary content]
--boundary123--";

            var requestOrResponse = new RequestOrResponse
            {
                Headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Content-Type", new[] { "multipart/form-data; boundary=boundary123" } }
                },
                Body = Encoding.UTF8.GetBytes(multipartBody)
            };

            // Act
            RecordEntry.NormalizeMultipartBody(requestOrResponse);

            // Assert - Only filename should be normalized, Content-Type should remain unchanged
            var actualNormalizedBody = Encoding.UTF8.GetString(requestOrResponse.Body);
            Assert.Equal(expectedNormalizedBody, actualNormalizedBody);
        }
    }
}
