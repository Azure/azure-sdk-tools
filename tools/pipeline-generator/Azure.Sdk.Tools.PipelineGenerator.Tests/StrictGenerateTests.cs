using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class StrictGenerateTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "strict-generate-" + Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("sdk/missing/ci.yml")]
        [InlineData("sdk/outside/ci.yml")]
        public async Task FailsBeforeAnyChangeWhenARequestedDescriptorIsNotFound(string missingPath)
        {
            var scanPath = Directory.CreateDirectory(Path.Combine(_root, "sdk", "service")).FullName;
            File.WriteAllText(Path.Combine(scanPath, "ci.yml"), "");
            // Exists on disk but outside the scanned directory, so it is out of scope for this request.
            Directory.CreateDirectory(Path.Combine(_root, "sdk", "outside"));
            File.WriteAllText(Path.Combine(_root, "sdk", "outside", "ci.yml"), "");
            var requestPath = Path.Combine(_root, "request.json");
            var resultsPath = Path.Combine(_root, "results.json");
            File.WriteAllText(requestPath, JsonSerializer.Serialize(new[]
            {
                new DescriptorRequest { Path = "sdk/service/ci.yml" },
                new DescriptorRequest { Path = missingPath }
            }));

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
                .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
                .AddTransient<SdkComponentScanner>()
                .BuildServiceProvider();
            var log = new CapturingLogger();
            var program = new Program(services, log);

            // An unreachable organization: without the up-front check, generation would fail later on a network call.
            var result = await program.RunAsync("https://unused.invalid", "internal", "net", scanPath, "Azure",
                "Azure/azure-sdk-for-net", "refs/heads/main", "Hosted", "up", Array.Empty<int>(), Array.Empty<string>(),
                "\\net", false, false, false, false, true, false, CancellationToken.None, requestPath, resultsPath, _root);

            Assert.Equal(ExitCondition.Exception, result);
            var error = Assert.IsType<ArgumentException>(log.Exception);
            Assert.Contains(missingPath, error.Message);
            Assert.DoesNotContain("sdk/service/ci.yml", error.Message);
            Assert.False(File.Exists(resultsPath));
        }

        private sealed class CapturingLogger : ILogger<Program>
        {
            public Exception Exception { get; private set; }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                Exception ??= exception;
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
