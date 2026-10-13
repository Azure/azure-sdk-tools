// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Mock.Tests;

[TestFixture]
public class ReleasePlanSpecCommitHandlerTests
{
    private const string SpecCommit = "0123456789abcdef0123456789abcdef01234567";

    [TestCase(SpecCommit, false)]
    [TestCase(SpecCommit, true)]
    [TestCase("", false)]
    [TestCase("", true)]
    public void Create_EchoesOptionalSpecCommit(string sha, bool wireArguments)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["typeSpecProjectPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager",
            ["specCommitSha"] = sha
        };

        var response = (ReleasePlanResponse)new CreateReleasePlanHandler().Handle(ToArguments(arguments, wireArguments));

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.ReleasePlanDetails!.SpecCommitSHA, Is.EqualTo(sha));
    }

    [TestCase(SpecCommit, 35000, false)]
    [TestCase(SpecCommit, 35000, true)]
    [TestCase("", 35000, false)]
    [TestCase("", 35000, true)]
    public void Update_EchoesOptionalSpecCommit(string sha, int workItemId, bool wireArguments)
    {
        var response = Update(sha, workItemId, wireArguments);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.ReleasePlanDetails!.WorkItemId, Is.EqualTo(35000));
        Assert.That(response.ReleasePlanDetails.SpecCommitSHA, Is.EqualTo(sha));
    }

    [TestCase(SpecCommit, 0, false)]
    [TestCase(SpecCommit, 0, true)]
    [TestCase(SpecCommit, -1, false)]
    [TestCase(SpecCommit, -1, true)]
    [TestCase(SpecCommit, 50001, false)]
    [TestCase(SpecCommit, 50001, true)]
    [TestCase("", 0, false)]
    [TestCase("", 0, true)]
    [TestCase("", -1, false)]
    [TestCase("", -1, true)]
    [TestCase("", 50001, false)]
    [TestCase("", 50001, true)]
    public void Update_RejectsWithoutExactMockWorkItem(string sha, int workItemId, bool wireArguments)
    {
        var response = Update(sha, workItemId, wireArguments);

        Assert.That(response.ResponseError, Is.Not.Null);
        Assert.That(response.ReleasePlanDetails, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Update_RejectsOmittedWorkItem(bool wireArguments)
    {
        var arguments = new Dictionary<string, object?>();
        var response = (ReleasePlanResponse)new UpdateReleasePlanHandler().Handle(ToArguments(arguments, wireArguments));

        Assert.That(response.ResponseError, Is.Not.Null);
        Assert.That(response.ReleasePlanDetails, Is.Null);
    }

    private static ReleasePlanResponse Update(string sha, int workItemId, bool wireArguments)
    {
        var arguments = new Dictionary<string, object?> { ["specCommitSha"] = sha, ["workItemId"] = workItemId };
        return (ReleasePlanResponse)new UpdateReleasePlanHandler().Handle(ToArguments(arguments, wireArguments));
    }

    private static Dictionary<string, object?> ToArguments(Dictionary<string, object?> arguments, bool wireArguments)
    {
        return wireArguments
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!
            : arguments;
    }
}
