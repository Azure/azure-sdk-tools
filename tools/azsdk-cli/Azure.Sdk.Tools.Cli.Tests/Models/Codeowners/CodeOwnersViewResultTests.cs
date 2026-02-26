using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Tests.Models.Codeowners;

[TestFixture]
public class CodeownersViewResultTests
{
    [Test]
    public void CodeownersViewResult_SplitsLabelOwners_IntoPathBasedAndPathless()
    {
        var pathBased = new LabelOwnerWorkItem { WorkItemId = 1, LabelType = "Service Owner", Repository = "repo", RepoPath = "/sdk/path" };
        var pathless = new LabelOwnerWorkItem { WorkItemId = 2, LabelType = "Azure SDK Owner", Repository = "repo", RepoPath = "" };

        var result = new CodeownersViewResult([], [pathBased, pathless]);

        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(1));
        Assert.That(result.PathBasedLabelOwners![0].WorkItemId, Is.EqualTo(1));
        Assert.That(result.PathlessLabelOwners, Has.Count.EqualTo(1));
        Assert.That(result.PathlessLabelOwners![0].WorkItemId, Is.EqualTo(2));
    }

    [Test]
    public void CodeownersViewResult_EmptyLists_SetsNulls()
    {
        var result = new CodeownersViewResult([], []);

        Assert.That(result.Packages, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Is.Null);
        Assert.That(result.PathlessLabelOwners, Is.Null);
    }

    [Test]
    public void CodeownersViewResult_ErrorResult_HasNoSections()
    {
        var result = new CodeownersViewResult { ResponseError = "Some error" };

        Assert.That(result.ResponseError, Is.EqualTo("Some error"));
        Assert.That(result.Packages, Is.Null);
        Assert.That(result.PathBasedLabelOwners, Is.Null);
        Assert.That(result.PathlessLabelOwners, Is.Null);
    }

    [Test]
    public void CodeownersViewResult_PathBasedLabelOwners_AreSortedByRepoThenPath()
    {
        var lo1 = new LabelOwnerWorkItem { WorkItemId = 1, LabelType = "Service Owner", Repository = "B-repo", RepoPath = "/z-path" };
        var lo2 = new LabelOwnerWorkItem { WorkItemId = 2, LabelType = "Service Owner", Repository = "A-repo", RepoPath = "/b-path" };
        var lo3 = new LabelOwnerWorkItem { WorkItemId = 3, LabelType = "Service Owner", Repository = "A-repo", RepoPath = "/a-path" };

        var result = new CodeownersViewResult([], [lo1, lo2, lo3]);

        Assert.That(result.PathBasedLabelOwners, Has.Count.EqualTo(3));
        Assert.That(result.PathBasedLabelOwners![0].Repository, Is.EqualTo("A-repo"));
        Assert.That(result.PathBasedLabelOwners[0].RepoPath, Is.EqualTo("/a-path"));
        Assert.That(result.PathBasedLabelOwners[1].Repository, Is.EqualTo("A-repo"));
        Assert.That(result.PathBasedLabelOwners[1].RepoPath, Is.EqualTo("/b-path"));
        Assert.That(result.PathBasedLabelOwners[2].Repository, Is.EqualTo("B-repo"));
    }
}
