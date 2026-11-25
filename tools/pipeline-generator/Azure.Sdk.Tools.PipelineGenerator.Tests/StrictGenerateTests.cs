using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
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
            var requests = new[]
            {
                new DescriptorRequest { Path = "sdk/service/ci.yml" },
                new DescriptorRequest { Path = missingPath }
            };
            var results = new List<PipelineGenerationResult>();

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
                "\\net", false, false, false, false, true, false, CancellationToken.None, requests, results, _root);

            Assert.Equal(ExitCondition.Exception, result);
            var error = Assert.IsType<ArgumentException>(log.Exception);
            Assert.Contains(missingPath, error.Message);
            Assert.DoesNotContain("sdk/service/ci.yml", error.Message);
            Assert.Empty(results);
        }

        [Fact]
        public async Task MissingServiceConnectionFailsBeforeAnyDefinitionChange()
        {
            var scanPath = Directory.CreateDirectory(Path.Combine(_root, "sdk", "service")).FullName;
            File.WriteAllText(Path.Combine(scanPath, "ci.yml"), "");
            var requests = new[] { new DescriptorRequest { Path = "sdk/service/ci.yml" } };
            var results = new List<PipelineGenerationResult>();
            var log = new CapturingLogger();
            var program = new FakeContextProgram(Services(), log);

            var result = await program.RunAsync("https://unused.invalid", "internal", "net", scanPath, "Azure",
                "Azure/azure-sdk-for-net", "refs/heads/main", "Hosted", "up", Array.Empty<int>(), new[] { "Azure", "Missing" },
                "\\net", false, false, false, false, true, false, CancellationToken.None, requests, results, _root);

            Assert.Equal(ExitCondition.Exception, result);
            var error = Assert.IsType<InvalidOperationException>(log.Exception);
            Assert.Contains("Missing", error.Message);
            // Definition lookup starts with the project reference; it must never be reached.
            Assert.False(program.Context.DefinitionLookupStarted);
            Assert.Empty(results);
        }

        [Fact]
        public async Task LegacyModeStillProcessesDefinitionsBeforeServiceConnections()
        {
            Directory.CreateDirectory(Path.Combine(_root, ".git"));
            var scanPath = Directory.CreateDirectory(Path.Combine(_root, "sdk", "service")).FullName;
            File.WriteAllText(Path.Combine(scanPath, "ci.yml"), "");
            var log = new CapturingLogger();
            var program = new FakeContextProgram(Services(), log);

            var result = await program.RunAsync("https://unused.invalid", "internal", "net", scanPath, "Azure",
                "Azure/azure-sdk-for-net", "refs/heads/main", "Hosted", "up", Array.Empty<int>(), new[] { "Azure", "Missing" },
                "\\net", false, false, false, false, true, false, CancellationToken.None);

            Assert.Equal(ExitCondition.Exception, result);
            Assert.Equal("Definition lookup must not start.", log.Exception.Message);
            Assert.True(program.Context.DefinitionLookupStarted);
            Assert.False(program.Context.ServiceConnectionLookupStarted);
        }

        [Fact]
        public void ReportsEveryMissingServiceConnection()
        {
            var found = new[] { new ServiceEndpoint { Name = "Azure" } };
            Assert.Null(Record.Exception(() => PipelineGenerationContext.EnsureAllServiceConnectionsFound(new[] { "Azure" }, found)));
            var error = Assert.Throws<InvalidOperationException>(() =>
                PipelineGenerationContext.EnsureAllServiceConnectionsFound(new[] { "Azure", "One", "Two" }, found));
            Assert.Contains("One, Two", error.Message);
        }

        private static ServiceProvider Services() => new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddTransient<SdkComponentScanner>()
            .BuildServiceProvider();

        private sealed class FakeContextProgram : Program
        {
            public FakeContextProgram(IServiceProvider services, ILogger<Program> logger) : base(services, logger) { }

            public FakeContext Context { get; private set; }

            protected override PipelineGenerationContext CreateContext(ILogger logger, string organization, string project,
                string endpoint, string repository, string branch, string agentPool, int[] variableGroups,
                string devOpsPath, string prefix, bool whatIf, bool noSchedule, bool setManagedVariables, bool overwriteTriggers)
                => Context = new FakeContext(logger, organization, project, endpoint, repository, branch, agentPool,
                    variableGroups, devOpsPath, prefix, whatIf, noSchedule, setManagedVariables, overwriteTriggers);
        }

        private sealed class FakeContext : PipelineGenerationContext
        {
            public FakeContext(ILogger logger, string organization, string project, string endpoint, string repository,
                string branch, string agentPool, int[] variableGroups, string devOpsPath, string prefix, bool whatIf,
                bool noSchedule, bool setManagedVariables, bool overwriteTriggers)
                : base(logger, organization, project, endpoint, repository, branch, agentPool, variableGroups, devOpsPath,
                    prefix, whatIf, noSchedule, setManagedVariables, overwriteTriggers) { }

            public bool DefinitionLookupStarted { get; private set; }

            public bool ServiceConnectionLookupStarted { get; private set; }

            public override Task<TeamProjectReference> GetProjectReferenceAsync(CancellationToken cancellationToken)
            {
                DefinitionLookupStarted = true;
                throw new InvalidOperationException("Definition lookup must not start.");
            }

            public override Task<IEnumerable<ServiceEndpoint>> GetServiceConnectionsAsync(IEnumerable<string> serviceConnections,
                CancellationToken cancellationToken)
            {
                ServiceConnectionLookupStarted = true;
                IEnumerable<ServiceEndpoint> found = new[] { new ServiceEndpoint { Name = "Azure" } };
                if (Strict)
                {
                    EnsureAllServiceConnectionsFound(serviceConnections, found);
                }
                return Task.FromResult(found);
            }
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
