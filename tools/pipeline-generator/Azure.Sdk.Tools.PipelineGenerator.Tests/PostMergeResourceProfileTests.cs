using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PipelineGenerator.Tests
{
    /// <summary>
    /// Guards against drift between the post-merge resource profile and the manual/nightly
    /// preparation job, which define the same per-language permissions in two places.
    /// </summary>
    public class PostMergeResourceProfileTests
    {
        private static readonly Lazy<string> Template = new(() =>
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "eng", "common", "pipelines", "templates", "jobs", "prepare-pipelines.yml");
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            throw new FileNotFoundException("Could not locate eng/common/pipelines/templates/jobs/prepare-pipelines.yml.");
        });

        [Theory]
        [InlineData("java")]
        [InlineData("js")]
        [InlineData("python")]
        [InlineData("net")]
        [InlineData("go")]
        public void MatchesManualAndNightlyPreparation(string language)
        {
            var template = Template.Value;
            var groupIds = Regex.Matches(template, @"^\s+(\w+):\s+(\d+)\s*$", RegexOptions.Multiline)
                .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
            var block = Regex.Match(template, $"\"{language}\"\\s*\\{{(?<body>[^}}]*)\\}}");
            Assert.True(block.Success, $"No '{language}' block in prepare-pipelines.yml.");
            var body = block.Groups["body"].Value;

            int[] Groups(string variable) => Regex.Matches(Assignment(body, variable) ?? "", @"\$\((\w+)\)")
                .Select(m => groupIds[m.Groups[1].Value]).Order().ToArray();

            var internalConnections = Connections(Assignment(template, "internalServiceConnections"))
                .Concat(Connections(Regex.Match(body, @"\$internalServiceConnections\s*\+=\s*'([^']*)'").Groups[1].Value));
            var profile = PostMergeResourceProfile.For(language);

            Assert.Equal(Groups("internalVariableGroups"), profile.InternalVariableGroups.Order());
            Assert.Equal(Groups("testVariableGroups"), profile.TestVariableGroups.Order());
            Assert.Equal(internalConnections.Order(), profile.InternalServiceConnections.Order());
            Assert.Equal(Connections(Assignment(template, "testServiceConnections")).Order(),
                profile.TestServiceConnections.Order());
        }

        private static string Assignment(string text, string variable)
        {
            var match = Regex.Match(text, $@"\${variable}\s*=\s*'([^']*)'");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static IEnumerable<string> Connections(string value) =>
            Regex.Matches(value ?? "", "\"([^\"]+)\"|(\\S+)")
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
    }
}
