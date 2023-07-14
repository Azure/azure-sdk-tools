using Azure.Sdk.Tools.TestProxy.Sanitizers;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        [Fact]
        public void SanitizerWorksAgainstSample()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/sample_entry.json");

            var uriRegexSanitizer = new BodyKeySanitizer(jsonPath: "$..trunks", value: "redacted.com");

            session.Session.Sanitize(uriRegexSanitizer);
            var newBody = Encoding.UTF8.GetString(session.Session.Entries[2].Response.Body);
            System.Console.WriteLine(newBody);
            Assert.Contains("redacted.com", newBody);
        }

        [Fact]
        public async Task APISanitizerWorksAgainstSample()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/sample_entry.json");

            // this is what your json body will look like coming over the wire. Notice the double escapes to prevent JSON parse break.
            // it is an identical sanitizer registration to the one above
            var overTheWire = "{ \"value\": \"redacted.com\", \"jsonPath\": \"$..trunks\" }";

            // Target the type of sanitizer using this. (This is similar to selecting a constructor above)
            var sanitizerName = "BodyKeySanitizer";


            #region API registration and running of sanitizer
            // feel free to ignore this setup, bunch of implementation details to register as if coming from external request
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Sanitizers.Clear();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = sanitizerName;
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(overTheWire);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;
            var controller = new Admin(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.AddSanitizer();
            var registeredSanitizer = testRecordingHandler.Sanitizers[0];
            Assert.NotNull(registeredSanitizer);
            #endregion

            session.Session.Sanitize(registeredSanitizer);
            var newBody = Encoding.UTF8.GetString(session.Session.Entries[2].Response.Body);
            Assert.Contains("redacted.com", newBody);
        }


        [Fact]
        public void SanitizerWorksAgainstSample1()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/sample_entry.json");

            var uriRegexSanitizer = new BodyKeySanitizer(jsonPath: "containerUri", value: "redacted", regex: "https://([a-zA-Z0-9-]{3,63}).blob.core.windows.net/[a-zA-Z0-9-]*\\?sv=.*");

            session.Session.Sanitize(uriRegexSanitizer);
            var newBody = Encoding.UTF8.GetString(session.Session.Entries[2].Response.Body);
            System.Console.WriteLine("Sample1");

           System.Console.WriteLine(newBody);
            Assert.Contains("redacted", newBody);
        }

        [Fact]
        public async Task APISanitizerWorksAgainstSample1()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/sample_entry.json");

            // this is what your json body will look like coming over the wire. Notice the double escapes to prevent JSON parse break.
            // it is an identical sanitizer registration to the one above
            var overTheWire = "{ \"value\": \"redacted\", \"regex\": \"https://([a-zA-Z0-9-]{3,63}).blob.core.windows.net/[a-zA-Z0-9-]*\\?sv=.*\", \"jsonPath\": \"containerUri\" }";

            // Target the type of sanitizer using this. (This is similar to selecting a constructor above)
            var sanitizerName = "BodyKeySanitizer";


            #region API registration and running of sanitizer
            // feel free to ignore this setup, bunch of implementation details to register as if coming from external request
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Sanitizers.Clear();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = sanitizerName;
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(overTheWire);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;
            var controller = new Admin(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.AddSanitizer();
            var registeredSanitizer = testRecordingHandler.Sanitizers[0];
            Assert.NotNull(registeredSanitizer);
            #endregion

            session.Session.Sanitize(registeredSanitizer);
            var newBody = Encoding.UTF8.GetString(session.Session.Entries[2].Response.Body);
            Assert.Contains("redacted", newBody);
        }
    }
}
