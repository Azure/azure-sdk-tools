using System;
using System.Text.Json.Nodes;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class PipelinePermissionTests
    {
        [Fact]
        public void AddsOnlyMissingPermissions()
        {
            var permissions = JsonNode.Parse("{\"pipelines\":[{\"id\":1,\"authorized\":true}]}");
            Assert.Equal(1, Program.AddPipelinePermissions(permissions, new[] { 1, 2, 2 }, strict: true));
            Assert.Equal(2, permissions["pipelines"].AsArray().Count);
            Assert.True(permissions["pipelines"][1]["authorized"].GetValue<bool>());
        }

        [Fact]
        public void LegacyModeLeavesExplicitRevocationsUnchanged()
        {
            var permissions = JsonNode.Parse("{\"pipelines\":[{\"id\":1,\"authorized\":false}]}");
            Assert.Equal(0, Program.AddPipelinePermissions(permissions, new[] { 1 }, strict: false));
            Assert.False(permissions["pipelines"][0]["authorized"].GetValue<bool>());
        }

        [Fact]
        public void StrictModeStopsInsteadOfOverridingRevocation()
        {
            var permissions = JsonNode.Parse("{\"pipelines\":[{\"id\":1,\"authorized\":false}]}");
            Assert.Throws<InvalidOperationException>(() =>
                Program.AddPipelinePermissions(permissions, new[] { 1 }, strict: true));
            Assert.False(permissions["pipelines"][0]["authorized"].GetValue<bool>());
        }
    }
}
