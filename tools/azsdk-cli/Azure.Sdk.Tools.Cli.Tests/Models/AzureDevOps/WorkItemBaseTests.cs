// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.Attributes;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Tests.Models.AzureDevOps;

[TestFixture]
public class WorkItemBaseTests
{
    private const string SystemTeamProjectIdEnvironmentVariableName = "SYSTEM_TEAMPROJECTID";

    [Test]
    public void GetPatchDocument_WithNullableDateTimeValue_RoundTripsDateFieldToJson()
    {
        var invalidSince = new DateTime(2026, 05, 29, 22, 55, 44, DateTimeKind.Utc);
        var workItem = new NullableDateWorkItem
        {
            InvalidSince = invalidSince
        };

        var operation = GetFieldOperation(workItem, "Custom.InvalidSince");
        var expectedValue = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(invalidSince))!;

        Assert.That(operation["Value"]?.GetValue<string>(), Is.EqualTo(expectedValue));
    }

    [Test]
    public void GetPatchDocument_WithNullNullableDateTime_RoundTripsEmptyStringToJson()
    {
        var workItem = new NullableDateWorkItem();

        var operation = GetFieldOperation(workItem, "Custom.InvalidSince");

        Assert.That(operation["Value"]?.GetValue<string>(), Is.EqualTo(string.Empty));
    }

    [TestCase(null)]
    [TestCase("")]
    public void GetPatchDocument_WhenAgentCreatedOutsidePipeline_SetsCreatedUsingToCopilot(string? teamProjectId)
    {
        var originalValue = Environment.GetEnvironmentVariable(SystemTeamProjectIdEnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(SystemTeamProjectIdEnvironmentVariableName, teamProjectId);
            var workItem = new WorkItemBase
            {
                IsCreatedByAgent = true
            };

            var operation = GetFieldOperation(workItem, "Custom.CreatedUsing");

            Assert.That(operation["Value"]?.GetValue<string>(), Is.EqualTo("Copilot"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SystemTeamProjectIdEnvironmentVariableName, originalValue);
        }
    }

    [Test]
    public void GetPatchDocument_WhenAgentCreatedInPipeline_SetsCreatedUsingToAutomation()
    {
        var originalValue = Environment.GetEnvironmentVariable(SystemTeamProjectIdEnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(SystemTeamProjectIdEnvironmentVariableName, Guid.NewGuid().ToString());
            var workItem = new WorkItemBase
            {
                IsCreatedByAgent = true
            };

            var operation = GetFieldOperation(workItem, "Custom.CreatedUsing");

            Assert.That(operation["Value"]?.GetValue<string>(), Is.EqualTo("Automation"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SystemTeamProjectIdEnvironmentVariableName, originalValue);
        }
    }

    private static JsonObject GetFieldOperation(WorkItemBase workItem, string fieldName)
    {
        var patchDocument = JsonSerializer.SerializeToNode(workItem.GetPatchDocument()) as JsonArray;
        Assert.That(patchDocument, Is.Not.Null);

        var operation = patchDocument!
            .OfType<JsonObject>()
            .Single(op => op["Path"]?.GetValue<string>() == $"/fields/{fieldName}");

        return operation!;
    }

    private sealed class NullableDateWorkItem : WorkItemBase
    {
        [FieldName("Custom.InvalidSince")]
        public DateTime? InvalidSince { get; set; }
    }
}
