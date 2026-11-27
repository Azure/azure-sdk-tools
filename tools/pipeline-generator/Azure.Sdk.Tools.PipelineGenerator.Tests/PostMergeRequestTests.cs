using System;
using System.Linq;
using System.Text.Json;
using CommandLine;
using PipelineGenerator.CommandParserOptions;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class PostMergeRequestTests
    {
        private static PreparePostMergeOptions Options(string repository = "azure-sdk-for-net",
            string descriptors = "[{\"path\":\"sdk/a/ci.yml\"}]") =>
            new PreparePostMergeOptions
            {
                RepositoryName = repository,
                MergeCommitSha = new string('a', 40),
                DescriptorPaths = descriptors
            };

        [Theory]
        [InlineData("java")]
        [InlineData("js")]
        [InlineData("python")]
        [InlineData("net")]
        [InlineData("go")]
        public void AcceptsInitialRepositories(string language)
        {
            Assert.Single(PostMergeRequest.Validate(Options($"azure-sdk-for-{language}")));
        }

        [Theory]
        [InlineData("azure-sdk-for-cpp")]
        [InlineData("azure-sdk-for-rust")]
        [InlineData("azure-sdk-for-net-pr")]
        [InlineData("Azure/azure-sdk-for-net")]
        public void RejectsUnsupportedRepositories(string repository)
        {
            Assert.Throws<ArgumentException>(() => PostMergeRequest.Validate(Options(repository)));
        }

        [Fact]
        public void ParsesDedicatedVerbWithNetDefaultWithoutChangingGenerate()
        {
            using var parser = new Parser(settings => settings.HelpWriter = null);
            var result = parser.ParseArguments<GenerateOptions, PreparePostMergeOptions>(
                new[] { "prepare-post-merge", "--merge-commit-sha", new string('a', 40),
                    "--descriptor-paths", "[{\"path\":\"sdk/a/ci.yml\"}]", "--sdk-directory", "sdk-data",
                    "--results-directory", "results" });
            var options = Assert.IsType<PreparePostMergeOptions>(result.Value);
            Assert.Equal("azure-sdk-for-net", options.RepositoryName);
            Assert.Single(PostMergeRequest.Validate(options));
            Assert.IsType<GenerateOptions>(parser.ParseArguments<GenerateOptions, PreparePostMergeOptions>(
                new[] { "generate", "--prefix", "net", "--repository", "Azure/azure-sdk-for-net",
                    "--path", "sdk", "--convention", "up" }).Value);
        }

        [Theory]
        [InlineData("main")]
        [InlineData("aaaaaaa")]
        [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n")]
        public void RequiresFullCanonicalSha(string sha)
        {
            var options = Options();
            options.MergeCommitSha = sha;
            Assert.Throws<ArgumentException>(() => PostMergeRequest.Validate(options));
        }

        [Theory]
        [InlineData("../sdk/a/ci.yml")]
        [InlineData("sdk/../a/ci.yml")]
        [InlineData("sdk/a/ci.foo-bar.yml")]
        [InlineData("sdk/a/ci.foo.bar.yml")]
        [InlineData("sdk/a/ci..yml")]
        [InlineData("sdk\\a\\ci.yml")]
        [InlineData("sdk/a/CI.yml")]
        [InlineData("sdk/a/ci.yml\n")]
        public void RejectsUnsafeOrScannerIncompatiblePaths(string path)
        {
            var descriptors = JsonSerializer.Serialize(new[] { new { path } });
            Assert.Throws<ArgumentException>(() => PostMergeRequest.Validate(Options(descriptors: descriptors)));
        }

        [Fact]
        public void AcceptsUnicodeWordVariantsAndRetainsRenameMapping()
        {
            var descriptors = "[{\"path\":\"sdk/new/ci.\u03b1\u0301.yml\",\"previousPath\":\"sdk/old/ci.yml\"}]";
            var request = Assert.Single(PostMergeRequest.Validate(Options(descriptors: descriptors)));
            Assert.Equal("sdk/old/ci.yml", request.PreviousPath);
            Assert.Equal("\u03b1\u0301", DescriptorRequest.ToComponent(request.Path).Variant);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("[null]")]
        [InlineData("[{}]")]
        [InlineData("[{\"path\":null}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\",\"previousPath\":null}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\",\"script\":\"untrusted\"}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\",\"path\":\"sdk/b/ci.yml\"}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\"},{\"path\":\"sdk/a/ci.yml\"}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\"},{\"path\":\"sdk/b/a/ci.yml\"}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\",\"previousPath\":\"sdk/a/ci.yml\"}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\",\"previousPath\":\"sdk/b/ci.yml\"},{\"path\":\"sdk/b/ci.yml\"}]")]
        [InlineData("[{\"path\":\"sdk/a/ci.yml\",\"previousPath\":\"sdk/old/ci.yml\"},{\"path\":\"sdk/b/ci.yml\",\"previousPath\":\"sdk/old/ci.yml\"}]")]
        public void RejectsInvalidShapeAndAmbiguousMappings(string descriptors)
        {
            Assert.Throws<ArgumentException>(() => PostMergeRequest.Validate(Options(descriptors: descriptors)));
        }

        [Fact]
        public void BoundsRequestSize()
        {
            var descriptors = Enumerable.Range(0, 100).Select(i => new { path = $"sdk/service{i}/ci.yml" }).ToList();
            Assert.Equal(100, PostMergeRequest.Validate(Options(descriptors: JsonSerializer.Serialize(descriptors))).Length);
            descriptors.Add(new { path = "sdk/extra/ci.yml" });
            Assert.Throws<ArgumentException>(() =>
                PostMergeRequest.Validate(Options(descriptors: JsonSerializer.Serialize(descriptors))));
        }

        [Fact]
        public void DeduplicatesRecursiveScansWithoutChangingTargets()
        {
            var requests = new[] { "sdk/a/ci.yml", "sdk/a/ci.one.yml", "sdk/a/nested/ci.yml", "sdk/ab/ci.yml" }
                .Select(p => new DescriptorRequest { Path = p }).ToArray();
            Assert.Equal(new[] { "sdk/a", "sdk/ab" }, PostMergeRequest.GetDirectories(requests));
            Assert.Equal(4, requests.Length);
        }

        [Theory]
        [InlineData("BUILD_REPOSITORY_NAME")]
        [InlineData("BUILD_SOURCEBRANCH")]
        [InlineData("SYSTEM_TEAMPROJECT")]
        [InlineData("SYSTEM_COLLECTIONURI")]
        public void RejectsAnyUntrustedExecutionContext(string changed)
        {
            string EnvironmentValue(string name) => name == changed ? "untrusted" : name switch
            {
                "BUILD_REPOSITORY_NAME" => "Azure/azure-sdk-tools",
                "BUILD_SOURCEBRANCH" => "refs/heads/main",
                "SYSTEM_TEAMPROJECT" => "internal",
                "SYSTEM_COLLECTIONURI" => "https://dev.azure.com/azure-sdk/",
                _ => null
            };
            Assert.Throws<InvalidOperationException>(() => PostMergeRequest.ValidateTrustedEnvironment(EnvironmentValue));
        }
    }
}
