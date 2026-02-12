

using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

[TestFixture]
public class WorkItemMappersTests
{    
    private static WorkItem CreatePackageWorkItem(int id, string packageName, params int[] relatedIds)
    {
        return CreatePackageWorkItem(id, packageName, "", relatedIds);
    }
    private static WorkItem CreatePackageWorkItem(int id, string packageName, string versionMajorMinor, params int[] relatedIds)
    {
        var wi = new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.Package", packageName },
                { "Custom.PackageVersionMajorMinor", versionMajorMinor }
            },
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
        return wi;
    }

    private static int createTestPackageId = 0;
    private static PackageWorkItem CreateTestPackage(string packageName, string version)
    {
        return new PackageWorkItem
        {
            WorkItemId = ++createTestPackageId,
            PackageName = packageName,
            PackageVersionMajorMinor = version,
        };
    }

    private static WorkItem CreateOwnerWorkItem(int id, string gitHubAlias)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.GitHubAlias", gitHubAlias }
            }
        };
    }

    private static WorkItem CreateLabelWorkItem(int id, string labelName)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.Label", labelName }
            }
        };
    }

    private static WorkItem CreateLabelOwnerWorkItem(int id, string labelType, string repository, string repoPath, params int[] relatedIds)
    {
        var wi = new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.LabelType", labelType },
                { "Custom.Repository", repository },
                { "Custom.RepoPath", repoPath }
            },
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
        return wi;
    }

    #region Package Model Tests

    [Test]
    public void PackageWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreatePackageWorkItem(100, "Azure.Storage.Blobs", 200, 300);

        var package = WorkItemMappers.MapToPackageWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(package.WorkItemId, Is.EqualTo(100));
            Assert.That(package.PackageName, Is.EqualTo("Azure.Storage.Blobs"));
            Assert.That(package.RelatedIds, Has.Count.EqualTo(2));
            Assert.That(package.RelatedIds, Does.Contain(200));
            Assert.That(package.RelatedIds, Does.Contain(300));
        });
    }

    [Test]
    public void PackageWorkItem_HandlesEmptyRelations()
    {
        var wi = new WorkItem
        {
            Id = 100,
            Fields = new Dictionary<string, object> { { "Custom.Package", "TestPackage" } },
            Relations = null
        };

        var package = WorkItemMappers.MapToPackageWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(package.WorkItemId, Is.EqualTo(100));
            Assert.That(package.PackageName, Is.EqualTo("TestPackage"));
            Assert.That(package.RelatedIds, Is.Empty);
        });
    }

    #endregion

    #region Owner Model Tests

    [Test]
    public void OwnerWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreateOwnerWorkItem(200, "johndoe");

        var owner = WorkItemMappers.MapToOwnerWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(owner.WorkItemId, Is.EqualTo(200));
            Assert.That(owner.GitHubAlias, Is.EqualTo("johndoe"));
        });
    }

    [Test]
    public void OwnerWorkItem_HandlesEmptyAlias()
    {
        var wi = new WorkItem
        {
            Id = 200,
            Fields = new Dictionary<string, object>()
        };

        var owner = WorkItemMappers.MapToOwnerWorkItem(wi);

        Assert.That(owner.GitHubAlias, Is.EqualTo(""));
    }

    #endregion

    #region Label Model Tests

    [Test]
    public void LabelWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreateLabelWorkItem(300, "Storage");

        var label = WorkItemMappers.MapToLabelWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(label.WorkItemId, Is.EqualTo(300));
            Assert.That(label.LabelName, Is.EqualTo("Storage"));
        });
    }

    #endregion

    #region LabelOwner Model Tests

    [Test]
    public void LabelOwnerWorkItem_MapsWorkItemCorrectly()
    {
        var wi = CreateLabelOwnerWorkItem(400, "Service Owner", "Azure/azure-sdk-for-net", "sdk/storage", 200, 300);

        var labelOwner = WorkItemMappers.MapToLabelOwnerWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(labelOwner.WorkItemId, Is.EqualTo(400));
            Assert.That(labelOwner.LabelType, Is.EqualTo("Service Owner"));
            Assert.That(labelOwner.Repository, Is.EqualTo("Azure/azure-sdk-for-net"));
            Assert.That(labelOwner.RepoPath, Is.EqualTo("sdk/storage"));
            Assert.That(labelOwner.RelatedIds, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void LabelOwnerWorkItem_HandlesEmptyRepoPath()
    {
        var wi = CreateLabelOwnerWorkItem(400, "Azure SDK Owner", "Azure/azure-sdk-for-net", "", 200);

        var labelOwner = WorkItemMappers.MapToLabelOwnerWorkItem(wi);

        Assert.Multiple(() =>
        {
            Assert.That(labelOwner.RepoPath, Is.EqualTo(""));
            Assert.That(labelOwner.LabelType, Is.EqualTo("Azure SDK Owner"));
        });
    }

    #endregion

    #region GetLatestPackageVersions Tests

    [Test]
    public void GetLatestPackageVersions_ReturnsLatestVersionForEachPackage()
    {
        // Arrange
        var packages = new List<PackageWorkItem>
        {
            CreateTestPackage("Package1", "0.1"),
            CreateTestPackage("Package1", "0.2"),
            CreateTestPackage("Package2", "1.0"),
            CreateTestPackage("Package2", "1.1"),
        };

        // Act
        var result = WorkItemMappers.GetLatestPackageVersions(packages);

        // Assert
        Assert.That(result.Single(
            p => p.PackageName == "Package1").PackageVersionMajorMinor,
            Is.EqualTo("0.2")
        );
        Assert.That(result.Single(
            p => p.PackageName == "Package2").PackageVersionMajorMinor,
            Is.EqualTo("1.1")
        );
    }

    #endregion

}
