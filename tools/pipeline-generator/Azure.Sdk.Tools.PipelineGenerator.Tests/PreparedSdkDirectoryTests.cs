using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using PipelineGenerator.CommandParserOptions;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class PreparedSdkDirectoryTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "prepared-sdk-" + Guid.NewGuid().ToString("N"));
        private readonly PreparePostMergeOptions _options;

        public PreparedSdkDirectoryTests()
        {
            _options = new PreparePostMergeOptions { SdkDirectory = Path.Combine(_directory, "source") };
            Directory.CreateDirectory(Path.Combine(_options.SdkDirectory, "sdk", "a"));
            File.WriteAllText(Path.Combine(_options.SdkDirectory, "sdk", "a", "ci.yml"), "");
        }

        [Fact]
        public void AcceptsPreparedDataAndScansWithoutGitMetadata()
        {
            PreparedSdkDirectory.Validate(_options, Targets());
            Assert.False(Directory.Exists(Path.Combine(_options.SdkDirectory, ".git")));
            var scanner = new SdkComponentScanner(NullLogger<SdkComponentScanner>.Instance);
            var component = Assert.Single(scanner.Scan(new DirectoryInfo(Path.Combine(_options.SdkDirectory, "sdk", "a")),
                "ci.yml", _options.SdkDirectory));
            Assert.Equal("sdk/a/ci.yml", component.RelativeYamlPath.Replace('\\', '/'));
        }

        [Fact]
        public void RejectsScanRootsOutsideThePreparedDirectory()
        {
            var outside = Directory.CreateDirectory(Path.Combine(_directory, "outside"));
            var scanner = new SdkComponentScanner(NullLogger<SdkComponentScanner>.Instance);
            Assert.Throws<ArgumentException>(() => scanner.Scan(outside, "ci.yml", _options.SdkDirectory).ToArray());
            Assert.Throws<InvalidOperationException>(() => PreparedSdkDirectory.Validate(_options,
                new[] { new DescriptorRequest { Path = "../outside/ci.yml" } }));
        }

        [Fact]
        public void RejectsMissingOrDirectoryDescriptors()
        {
            var path = Path.Combine(_options.SdkDirectory, "sdk", "a", "ci.yml");
            File.Delete(path);
            Assert.Throws<InvalidOperationException>(() => PreparedSdkDirectory.Validate(_options, Targets()));
            Directory.CreateDirectory(path);
            Assert.Throws<InvalidOperationException>(() => PreparedSdkDirectory.Validate(_options, Targets()));
        }

        [Fact]
        public void RequiresOldDescriptorToBeAbsentFromPreparedContent()
        {
            var targets = new[] { new DescriptorRequest { Path = "sdk/a/ci.yml", PreviousPath = "sdk/old/ci.yml" } };
            PreparedSdkDirectory.Validate(_options, targets);
            Directory.CreateDirectory(Path.Combine(_options.SdkDirectory, "sdk", "old"));
            File.WriteAllText(Path.Combine(_options.SdkDirectory, "sdk", "old", "ci.yml"), "");
            var error = Assert.Throws<InvalidOperationException>(() => PreparedSdkDirectory.Validate(_options, targets));
            Assert.Contains("still exists", error.Message);
        }

        [Theory]
        [InlineData("root")]
        [InlineData("ancestor")]
        [InlineData("descriptor")]
        [InlineData("nested")]
        [InlineData("previous")]
        public void RejectsLinksInRootsDescriptorsAndRecursiveScanScopes(string location)
        {
            // Directory junctions avoid requiring Windows symlink privileges; Linux uses ordinary symlinks.
            var outside = Path.Combine(_directory, "outside");
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "ci.yml"), "");
            var linkedDirectory = location == "root" ? Path.Combine(_directory, "linked-root") :
                location == "ancestor" ? Path.Combine(_options.SdkDirectory, "sdk", "linked") :
                location == "previous" ? Path.Combine(_options.SdkDirectory, "sdk", "old") :
                Path.Combine(_options.SdkDirectory, "sdk", "a", "nested");
            if (location == "descriptor")
            {
                var target = Path.Combine(_options.SdkDirectory, "sdk", "a", "ci.yml");
                File.Delete(target);
                // A directory link in place of a descriptor must be rejected before traversing it.
                linkedDirectory = target;
            }
            CreateDirectoryLink(linkedDirectory, outside);
            if (location == "root")
            {
                _options.SdkDirectory = linkedDirectory;
            }
            var targets = location switch
            {
                "ancestor" => new[] { new DescriptorRequest { Path = "sdk/linked/ci.yml" } },
                "previous" => new[] { new DescriptorRequest { Path = "sdk/a/ci.yml", PreviousPath = "sdk/old/ci.yml" } },
                _ => Targets()
            };
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() => PreparedSdkDirectory.Validate(_options, targets));
                Assert.Contains("links or reparse points", error.Message);
            }
            finally
            {
                Directory.Delete(linkedDirectory);
            }
        }

        private static void CreateDirectoryLink(string path, string target)
        {
            if (OperatingSystem.IsWindows())
            {
                // Directory.CreateSymbolicLink requires Developer Mode on Windows, so use a local junction.
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                    $"/c mklink /J \"{path}\" \"{target}\"")
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }
            else
            {
                Directory.CreateSymbolicLink(path, target);
            }
        }

        private static DescriptorRequest[] Targets() => new[] { new DescriptorRequest { Path = "sdk/a/ci.yml" } };

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
