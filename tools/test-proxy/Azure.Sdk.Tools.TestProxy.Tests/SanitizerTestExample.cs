using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Xunit;
using Azure.Core;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    /// <summary>
    /// This test is provided as a preface to a "real" response in helping folks understand why their regex aren't working as they expect
    /// 
    /// Users should modify "Test.RecordEntries/sample_entry.json" to match their request or response, then use the function below to test
    /// the regex they are attempting to register.
    /// 
    /// Below a generalRegexSanitizer is being used, feel free to replace with any sanitizer provided in Azure.Sdk.Tools.TestProxy.Sanitizers.
    /// </summary>
    public class SanitizerTestExample
    {

        public static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        [Fact]
        public void TestSetRecordingOptionsValidTlsCert()
        {
            var certValue = "-----BEGIN CERTIFICATE-----MIIBgTCCASegAwIBAgIRAP8o8bVU8taW6SIlq68ooFAwCgYIKoZIzj0EAwIwFjEUMBIGA1UEAwwLQ0NGIE5ldHdvcmswHhcNMjMwNzE5MTQzNTM4WhcNMjMxMDE3MTQzNTM3WjAWMRQwEgYDVQQDDAtDQ0YgTmV0d29yazBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABD4ujJba2GkR0bAD+AS+dbUBenPAC6iqXJbM2q+JJWCN1O/GdUfmVZagan5OQxn417cKp4dGiExyVpEdeg0/LyKjVjBUMBIGA1UdEwEB/wQIMAYBAf8CAQAwHQYDVR0OBBYEFBHQ0lGEDifiYVaYfZkjOCLf2maTMB8GA1UdIwQYMBaAFBHQ0lGEDifiYVaYfZkjOCLf2maTMAoGCCqGSM49BAMCA0gAMEUCIDyeyrpYZLGrklG9Z1jyaKX0U/P5CBmL2jE+1boYEFeyAiEA/hPrtNfhdYX9JrVz8MDWzlojkCClSGwbjn1HZMW/wNY=-----END CERTIFICATE-----";
            var inputObj = string.Format("{{\"Transport\": {{\"TLSValidationCert\": \"{0}\"}}}}", certValue);
            var testRecordingHandler = new RecordingHandler(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            testRecordingHandler.SetRecordingOptions(inputBody, null);
        }    
    }
}
