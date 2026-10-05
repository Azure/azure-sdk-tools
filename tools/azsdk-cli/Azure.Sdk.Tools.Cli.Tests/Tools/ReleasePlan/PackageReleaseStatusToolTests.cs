// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class PackageReleaseStatusToolTests
    {
        private const string PythonSdkPr = "https://github.com/Azure/azure-sdk-for-python/pull/100";
        private Mock<IDevOpsService> _devOps = null!;
        private PackageReleaseStatusTool _tool = null!;
        private ReleasePlanWorkItem _plan = null!;
        private readonly List<(int Id, Dictionary<string, string> Fields, int Revision)> _writes = [];

        [SetUp]
        public void Setup()
        {
            _writes.Clear();
            _plan = CreatePlan();
            _devOps = new Mock<IDevOpsService>();
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(100, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [_plan]);
            _devOps.Setup(s => s.GetReleasePlansBySdkPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [_plan]);
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _plan);
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _devOps.Setup(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, Dictionary<string, string>, int, CancellationToken>((id, fields, revision, _) =>
                {
                    _writes.Add((id, new Dictionary<string, string>(fields), revision));
                    foreach (var sdk in _plan.SDKInfo)
                    {
                        var languageId = DevOpsService.MapLanguageToId(sdk.Language);
                        if (fields.TryGetValue($"Custom.ReleaseStatusFor{languageId}", out var status))
                        {
                            sdk.ReleaseStatus = status;
                        }
                        if (fields.TryGetValue($"Custom.ReleasedVersionFor{languageId}", out var version))
                        {
                            sdk.ReleasedVersion = version;
                        }
                    }
                    if (fields.TryGetValue("System.State", out var state))
                    {
                        _plan.Status = state;
                    }
                    _plan.Revision++;
                })
                .ReturnsAsync(() => new WorkItem { Id = _plan.WorkItemId, Rev = _plan.Revision });
            var notificationService = new Mock<INotificationService>();
            notificationService.Setup(s => s.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(NotificationResult.Sent);
            _tool = new PackageReleaseStatusTool(_devOps.Object, new TestLogger<PackageReleaseStatusTool>(), notificationService.Object);
        }

        [TearDown]
        public void NeverUsesHeuristicLookupOrUnguardedWrites()
        {
            Assert.That(_devOps.Invocations.Where(i => i.Method.Name is nameof(IDevOpsService.ResolveReleasePlanByIdAsync)
                or nameof(IDevOpsService.GetReleasePlanAsync)), Is.Empty);
            Assert.That(_devOps.Invocations.Where(i => i.Method.Name == nameof(IDevOpsService.UpdateWorkItemAsync)
                && (i.Arguments.Count != 4 || i.Arguments[2] is not int)), Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task CorrelationRegression_WithoutValidPlanId_DoesNotWrite(int releasePlanId)
        {
            _devOps.Setup(s => s.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var result = await UpdateAsync(releasePlanId: releasePlanId, version: "7.0.1");

            AssertNoWrites();
            if (releasePlanId == 0)
            {
                Assert.That(_devOps.Invocations.Select(i => i.Method.Name), Is.EqualTo(new[] { nameof(IDevOpsService.GetReleasePlansForPackageAsync) }),
                    "Without a plan ID or SDK PR only the transitional package lookup runs.");
                Assert.That(result.ResponseError, Is.Null);
                Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            }
            else
            {
                Assert.That(_devOps.Invocations, Is.Empty);
                Assert.That(result.ResponseError, Does.Contain("positive integer"));
            }
        }

        [Test]
        public async Task PlanIdNotFound_DoesNotFallBackToLegacyLookup()
        {
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(999, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var result = await UpdateAsync(releasePlanId: 999);

            AssertNoWrites();
            Assert.That(result.ResponseError, Does.Contain("exactly one release plan"));
            _devOps.Verify(s => s.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CorrelationRegression_DuplicatePlanId_DoesNotWrite(bool differentPackage)
        {
            var duplicate = CreatePlan();
            duplicate.WorkItemId = 22222;
            if (differentPackage)
            {
                duplicate.SDKInfo.Single(s => s.Language == "Python").PackageName = "azure-other";
            }
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(100, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([_plan, duplicate]);

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Does.Contain("exactly one").And.Contain("12345").And.Contain("22222"));
            AssertNoWrites();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public async Task InvalidPackageName_DoesNotWrite(string? packageName)
        {
            var result = await UpdateAsync(packageName: packageName!);
            Assert.That(result.ResponseError, Does.Contain("Package name cannot be null or empty"));
            Assert.That(_devOps.Invocations, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public async Task InvalidLanguage_DoesNotWrite(string? language)
        {
            var result = await UpdateAsync(language: language!);
            Assert.That(result.ResponseError, Does.Contain("Language cannot be null or empty"));
            Assert.That(_devOps.Invocations, Is.Empty);
        }

        [TestCase("rust")]
        [TestCase("cpp")]
        [TestCase("unknown")]
        public async Task UnsupportedLanguage_DoesNotWrite(string language)
        {
            var result = await UpdateAsync(language: language);
            Assert.That(result.Message, Does.Contain("not supported"));
            Assert.That(_devOps.Invocations, Is.Empty);
        }

        [TestCase(".NET", "Azure.Test", "Dotnet", SdkLanguage.DotNet)]
        [TestCase("dotnet", "Azure.Test", "Dotnet", SdkLanguage.DotNet)]
        [TestCase("csharp", "Azure.Test", "Dotnet", SdkLanguage.DotNet)]
        [TestCase("c#", "Azure.Test", "Dotnet", SdkLanguage.DotNet)]
        [TestCase("JavaScript", "@azure/test", "JavaScript", SdkLanguage.JavaScript)]
        [TestCase("js", "@azure/test", "JavaScript", SdkLanguage.JavaScript)]
        [TestCase("typescript", "@azure/test", "JavaScript", SdkLanguage.JavaScript)]
        [TestCase("Python", "azure-test", "Python", SdkLanguage.Python)]
        [TestCase("PYTHON", "azure-test", "Python", SdkLanguage.Python)]
        [TestCase(" python ", "azure-test", "Python", SdkLanguage.Python)]
        [TestCase("Java", "azure-test", "Java", SdkLanguage.Java)]
        [TestCase("Go", "sdk/test/aztest", "Go", SdkLanguage.Go)]
        public async Task SamePlanWithManyLanguages_UpdatesOnlySelectedEntry(string language, string packageName, string fieldId, SdkLanguage expectedLanguage)
        {
            var result = await UpdateAsync(language: language, packageName: packageName);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Language, Is.EqualTo(expectedLanguage));
            Assert.That(result.ReleasePlanId, Is.EqualTo(100));
            Assert.That(result.TypeSpecProject, Is.EqualTo(_plan.APISpecProjectPath));
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            Assert.That(_writes, Has.Count.EqualTo(1));
            Assert.That(_writes[0].Id, Is.EqualTo(12345), "Write using the resolved work item ID, not the display ID.");
            Assert.That(_writes[0].Revision, Is.EqualTo(7));
            Assert.That(_writes[0].Fields, Is.EquivalentTo(new Dictionary<string, string> { [$"Custom.ReleaseStatusFor{fieldId}"] = "Released" }));
            Assert.That(_plan.SDKInfo.Count(s => s.ReleaseStatus == "Released"), Is.EqualTo(1));
        }

        [TestCase("package")]
        [TestCase("package-case")]
        [TestCase("id")]
        [TestCase("work-item-id")]
        [TestCase("environment")]
        [TestCase("revision")]
        [TestCase("missing-language")]
        [TestCase("duplicate-language")]
        [TestCase("two-packages-in-language")]
        [TestCase("cross-language-package")]
        public async Task ConflictingPlanData_DoesNotWrite(string conflict)
        {
            var python = _plan.SDKInfo.Single(s => s.Language == "Python");
            switch (conflict)
            {
                case "package": python.PackageName = "azure-other"; break;
                case "package-case": python.PackageName = "Azure-Test"; break;
                case "id": _plan.ReleasePlanId = 200; break;
                case "work-item-id": _plan.WorkItemId = 0; break;
                case "environment": _plan.IsTestReleasePlan = true; break;
                case "revision": _plan.Revision = 0; break;
                case "missing-language": _plan.SDKInfo.Remove(python); break;
                case "duplicate-language": _plan.SDKInfo.Add(new SDKInfo { Language = "python", PackageName = "azure-test" }); break;
                case "two-packages-in-language": _plan.SDKInfo.Add(new SDKInfo { Language = "Python", PackageName = "azure-other" }); break;
                case "cross-language-package": python.PackageName = "azure-other"; break; // Java still has azure-test.
            }

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Does.Contain("No release plan updated"));
            Assert.That(result.ReleaseStatus, Is.Empty);
            AssertNoWrites();
        }

        [TestCase("New")]
        [TestCase("Not Started")]
        [TestCase("Abandoned")]
        [TestCase("Closed")]
        [TestCase("Duplicate")]
        [TestCase("Finished")]
        public async Task NonActivePlan_DoesNotWrite(string state)
        {
            _plan.Status = state;
            var result = await UpdateAsync();
            Assert.That(result.ResponseError, Is.Not.Null);
            AssertNoWrites();
        }

        [TestCase(200)]
        [TestCase(12345)]
        public async Task UnresolvedId_NeverFallsBackToOtherPlanOrWorkItemId(int releasePlanId)
        {
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(releasePlanId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            var result = await UpdateAsync(releasePlanId: releasePlanId);
            Assert.That(result.ResponseError, Does.Contain("found 0"));
            AssertNoWrites();
        }

        [TestCase(null, null)]
        [TestCase("", "")]
        [TestCase("1.2.3", null)]
        [TestCase(null, "https://dev.azure.com/azure-sdk/internal/_build?buildId=123")]
        [TestCase("1.2.3", "https://dev.azure.com/azure-sdk/internal/_build?buildId=123")]
        public async Task OptionalResultMetadata_IsRecordedOnlyWhenPresent(string? version, string? pipeline)
        {
            var result = await UpdateAsync(version: version, pipeline: pipeline);
            Assert.That(result.ResponseError, Is.Null);
            var fields = _writes.Single().Fields;
            Assert.That(fields.ContainsKey("Custom.ReleasedVersionForPython"), Is.EqualTo(!string.IsNullOrWhiteSpace(version)));
            Assert.That(fields.ContainsKey("Custom.ReleasePipelineForPython"), Is.EqualTo(!string.IsNullOrWhiteSpace(pipeline)));
            if (!string.IsNullOrWhiteSpace(version)) Assert.That(fields["Custom.ReleasedVersionForPython"], Is.EqualTo(version));
            if (!string.IsNullOrWhiteSpace(pipeline)) Assert.That(fields["Custom.ReleasePipelineForPython"], Is.EqualTo(pipeline));
        }

        [TestCase("beta", false)]
        [TestCase("STABLE", true)]
        public async Task SuppliedReleaseType_ValidatesRatherThanSelects(string releaseType, bool matches)
        {
            var result = await UpdateAsync(sdkReleaseType: releaseType);
            Assert.That(result.ResponseError is null, Is.EqualTo(matches));
            Assert.That(_writes.Count, Is.EqualTo(matches ? 1 : 0));
        }

        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/100", true)]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/200", false)]
        public async Task SuppliedSdkPr_ValidatesSelectedLanguage(string sdkPr, bool matches)
        {
            _plan.SDKInfo.Single(s => s.Language == "Python").SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/100";
            _plan.SDKInfo.Single(s => s.Language == "Java").SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/200";
            var result = await UpdateAsync(sdkPr: sdkPr);
            Assert.That(result.ResponseError is null, Is.EqualTo(matches));
            Assert.That(_writes.Count, Is.EqualTo(matches ? 1 : 0));
        }

        [TestCase("Pending")]
        [TestCase("Release In Progress")]
        public async Task NonReleasedStatus_RequiresSameCorrelationButDoesNotFinish(string status)
        {
            var result = await UpdateAsync(status: status, version: "1.2.3");
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo(status));
            Assert.That(_writes.Single().Fields["Custom.ReleaseStatusForPython"], Is.EqualTo(status));
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase("In Progress")]
        [TestCase("Finished")]
        public async Task RepeatedRelease_IsNoOpEvenAfterPlanFinished(string state)
        {
            _plan.Status = state;
            var python = _plan.SDKInfo.Single(s => s.Language == "Python");
            python.ReleaseStatus = "Released";
            python.ReleasedVersion = "1.2.3";
            var result = await UpdateAsync(version: "1.2.3");
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("already marked Released"));
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()),
                state == "Finished" ? Times.Never() : Times.Once());
            AssertNoWrites();
        }

        [TestCase("Released", "2.0.0")]
        [TestCase("Pending", "1.2.3")]
        public async Task ExistingRelease_CannotBeOverwritten(string status, string version)
        {
            var python = _plan.SDKInfo.Single(s => s.Language == "Python");
            python.ReleaseStatus = "Released";
            python.ReleasedVersion = "1.2.3";
            var result = await UpdateAsync(status: status, version: version);
            Assert.That(result.ResponseError, Does.Contain("cannot be overwritten"));
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            AssertNoWrites();
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public async Task LastRequiredLanguage_FinishesUsingFreshRevision(bool management, bool excludeJava)
        {
            _plan.IsManagementPlane = management;
            _plan.IsDataPlane = !management;
            foreach (var sdk in _plan.SDKInfo.Where(s => s.Language != "Python"))
            {
                sdk.ReleaseStatus = "Released";
            }
            if (excludeJava)
            {
                var java = _plan.SDKInfo.Single(s => s.Language == "Java");
                java.ReleaseStatus = "Pending";
                java.ReleaseExclusionStatus = "Approved";
            }
            if (!management) _plan.SDKInfo.Single(s => s.Language == "Go").ReleaseStatus = "Pending";

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(_writes, Has.Count.EqualTo(2));
            Assert.That(_writes[0].Revision, Is.EqualTo(7));
            Assert.That(_writes[1].Revision, Is.EqualTo(8));
            Assert.That(_writes[1].Fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Finished" }));
        }

        [Test]
        public async Task CompletionRefreshFailure_DoesNotUndoSuccessfulReleaseStatus()
        {
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Read failed"));
            var result = await UpdateAsync();
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.Message, Does.Contain("failed to auto-finish"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            Assert.That(_writes, Has.Count.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CompletionRetryRegression_RetriesFinishAfterRefreshFailure(bool automatic)
        {
            foreach (var sdk in _plan.SDKInfo.Where(s => s.Language != "Python")) sdk.ReleaseStatus = "Released";
            _plan.SDKInfo.Single(s => s.Language == "Python").SdkPullRequestUrl = PythonSdkPr;
            _devOps.SetupSequence(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Temporary refresh failure"))
                .ReturnsAsync(_plan);
            var id = automatic ? 0 : 100;
            var sdkPr = automatic ? PythonSdkPr : null;

            var initial = await UpdateAsync(releasePlanId: id, sdkPr: sdkPr, version: "1.2.3", pipeline: "https://example.test/build/1");
            var retry = await UpdateAsync(releasePlanId: id, sdkPr: sdkPr, version: "1.2.3", pipeline: "https://example.test/build/2");

            Assert.That(initial.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(initial.Message, Does.Contain("failed to auto-finish"));
            Assert.That(retry.ResponseError, Is.Null);
            Assert.That(retry.ReleasePlanFinished, Is.True);
            Assert.That(_writes, Has.Count.EqualTo(2));
            Assert.That(_writes[0].Fields["Custom.ReleasePipelineForPython"], Is.EqualTo("https://example.test/build/1"));
            Assert.That(_writes[1].Fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Finished" }));
            Assert.That(_writes[1].Revision, Is.EqualTo(8));
            _devOps.Verify(s => s.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(f => f.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task CompletionRetry_RetriesFailedFinishedWriteWithoutRewritingReleaseFields()
        {
            foreach (var sdk in _plan.SDKInfo.Where(s => s.Language != "Python")) sdk.ReleaseStatus = "Released";
            _devOps.SetupSequence(s => s.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(f => f.ContainsKey("System.State")), 8, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Temporary Finished write failure"))
                .ReturnsAsync(new WorkItem { Id = 12345, Rev = 9 });

            var initial = await UpdateAsync(version: "1.2.3");
            var retry = await UpdateAsync(version: "1.2.3");

            Assert.That(initial.ReleasePlanFinished, Is.False);
            Assert.That(initial.Message, Does.Contain("failed to auto-finish"));
            Assert.That(retry.ResponseError, Is.Null);
            Assert.That(retry.ReleasePlanFinished, Is.True);
            _devOps.Verify(s => s.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(f => f.ContainsKey("Custom.ReleaseStatusForPython")), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(f => f.Count == 1 && f.ContainsKey("System.State") && f["System.State"] == "Finished"), 8, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task CompletionRetry_UsesTheFreshRevision()
        {
            foreach (var sdk in _plan.SDKInfo) sdk.ReleaseStatus = "Released";
            var current = CreatePlan();
            foreach (var sdk in current.SDKInfo) sdk.ReleaseStatus = "Released";
            current.Revision = 12;
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(current);

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(_writes.Single().Revision, Is.EqualTo(12));
            Assert.That(_writes.Single().Fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Finished" }));
        }

        [TestCase("package")]
        [TestCase("id")]
        [TestCase("revision")]
        [TestCase("state")]
        [TestCase("incomplete")]
        public async Task CompletionRetry_RevalidatesTheFreshPlanBeforeFinishing(string change)
        {
            foreach (var sdk in _plan.SDKInfo) sdk.ReleaseStatus = "Released";
            var current = CreatePlan();
            foreach (var sdk in current.SDKInfo) sdk.ReleaseStatus = "Released";
            switch (change)
            {
                case "package": current.SDKInfo.Single(s => s.Language == "Python").PackageName = "another-package"; break;
                case "id": current.ReleasePlanId = 200; break;
                case "revision": current.Revision = 0; break;
                case "state": current.Status = "Abandoned"; break;
                case "incomplete": current.SDKInfo.Single(s => s.Language == "Java").ReleaseStatus = "Pending"; break;
            }
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(current);

            var result = await UpdateAsync();

            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()), Times.Once);
            AssertNoWrites();
        }

        [Test]
        public void CompletionRetry_CallerCancellationDuringRefreshPreventsWrite()
        {
            foreach (var sdk in _plan.SDKInfo) sdk.ReleaseStatus = "Released";
            using var cancellation = new CancellationTokenSource();
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, cancellation.Token))
                .Callback(() => cancellation.Cancel()).ReturnsAsync(_plan);

            Assert.CatchAsync<OperationCanceledException>(() => UpdateAsync(ct: cancellation.Token));

            AssertNoWrites();
        }

        [TestCase("release-type")]
        [TestCase("package")]
        [TestCase("plane")]
        [TestCase("project")]
        public async Task ChangedTargetAfterStatusUpdate_DoesNotFinish(string change)
        {
            var changedPlan = CreatePlan();
            switch (change)
            {
                case "release-type": changedPlan.SDKReleaseType = "beta"; break;
                case "package": changedPlan.SDKInfo.Single(s => s.Language == "Python").PackageName = "azure-other"; break;
                case "plane": changedPlan.IsManagementPlane = false; changedPlan.IsDataPlane = true; break;
                case "project": changedPlan.APISpecProjectPath = "specification/other/project"; break;
            }
            foreach (var sdk in changedPlan.SDKInfo) sdk.ReleaseStatus = "Released";
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(changedPlan);
            var result = await UpdateAsync();
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("plan changed"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            Assert.That(_writes, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RevisionConflict_ReturnsErrorWithoutRetryOrCompletion()
        {
            _devOps.Setup(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Revision conflict"));
            var result = await UpdateAsync();
            Assert.That(result.ResponseError, Does.Contain("Revision conflict"));
            Assert.That(result.ReleaseStatus, Is.Empty);
            _devOps.Verify(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task CompletionRevisionConflict_PreservesStatusAndDoesNotRetry()
        {
            foreach (var sdk in _plan.SDKInfo.Where(s => s.Language != "Python")) sdk.ReleaseStatus = "Released";
            _devOps.Setup(s => s.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(f => f.ContainsKey("System.State")), 8, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Revision conflict"));

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(result.Message, Does.Contain("failed to auto-finish"));
            Assert.That(result.ReleasePlanFinished, Is.False);
            _devOps.Verify(s => s.UpdateWorkItemAsync(12345, It.Is<Dictionary<string, string>>(f => f.ContainsKey("System.State")), 8, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task LookupFailure_IsDiagnosticNotNoPlanSuccess()
        {
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(100, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Lookup failed"));
            var result = await UpdateAsync();
            Assert.That(result.ResponseError, Does.Contain("Lookup failed").And.Contain("100").And.Contain("azure-test"));
            AssertNoWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CallerCancellation_StopsBeforeWriting(bool cancelDuringLookup)
        {
            using var cancellation = new CancellationTokenSource();
            if (cancelDuringLookup)
            {
                _devOps.Setup(s => s.GetReleasePlansByIdAsync(100, It.IsAny<bool>(), cancellation.Token))
                    .Callback(() => cancellation.Cancel()).ReturnsAsync([_plan]);
            }
            else cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => UpdateAsync(ct: cancellation.Token));
            AssertNoWrites();
        }

        [Test]
        public async Task Cli_ForwardsAllCorrelationAndResultInputs()
        {
            var command = _tool.GetCommandInstances().First();
            var parse = command.Parse("--package-name @azure/test --language JavaScript --release-plan-id 100 --package-version 1.2.3 --sdk-release-type stable --release-pipeline https://example.test/build/1",
                new CommandLineConfiguration(command) { ResponseFileTokenReplacer = null });
            Assert.That(parse.Errors, Is.Empty);

            var result = (ReleaseStatusUpdateResponse)await _tool.HandleCommand(parse, CancellationToken.None);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(_writes.Single().Fields["Custom.ReleaseStatusForJavaScript"], Is.EqualTo("Released"));
            Assert.That(_writes.Single().Fields["Custom.ReleasedVersionForJavaScript"], Is.EqualTo("1.2.3"));
            Assert.That(_writes.Single().Fields["Custom.ReleasePipelineForJavaScript"], Is.EqualTo("https://example.test/build/1"));
        }

        [Test]
        public async Task IdOnlyRegression_ManualReleaseDoesNotRequireApiVersion()
        {
            _plan.SpecAPIVersion = string.Empty;
            var command = _tool.GetCommandInstances().First();
            var parse = command.Parse("--package-name azure-test --language Python --release-plan-id 100");

            var result = (ReleaseStatusUpdateResponse)await _tool.HandleCommand(parse, CancellationToken.None);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(_writes.Single().Fields["Custom.ReleaseStatusForPython"], Is.EqualTo("Released"));
        }

        [Test]
        public async Task Cli_OmittedCorrelationInputs_UsesLegacyLookup()
        {
            _devOps.Setup(s => s.GetReleasePlansForPackageAsync("azure-test", "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            var command = _tool.GetCommandInstances().First();
            var parse = command.Parse("--package-name azure-test --language Python");
            Assert.That(parse.Errors, Is.Empty);
            var result = (ReleaseStatusUpdateResponse)await _tool.HandleCommand(parse, CancellationToken.None);
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            Assert.That(_devOps.Invocations.Select(i => i.Method.Name), Is.EqualTo(new[] { nameof(IDevOpsService.GetReleasePlansForPackageAsync) }));
        }

        [TestCase(".NET", "Azure.Test", "Dotnet")]
        [TestCase("Java", "azure-test", "Java")]
        [TestCase("JavaScript", "@azure/test", "JavaScript")]
        [TestCase("Python", "azure-test", "Python")]
        [TestCase("Go", "sdk/test/aztest", "Go")]
        public async Task AutomaticSdkPr_UsesAdoPlanIdAndOnlyTheMatchingLanguage(string language, string packageName, string fieldId)
        {
            var sdkPr = $"https://github.com/Azure/{SdkLanguageHelpers.GetRepoName(SdkLanguageHelpers.GetSdkLanguage(language))}/pull/100";
            _plan.SDKInfo.Single(s => s.Language == language).SdkPullRequestUrl = sdkPr;

            var result = await UpdateAsync(releasePlanId: 0, language: language, packageName: packageName, sdkPr: sdkPr);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanId, Is.EqualTo(100), "Read Custom.ReleasePlanID, not WorkItemId (12345).");
            _devOps.Verify(s => s.GetReleasePlansBySdkPullRequestAsync(sdkPr, language, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_writes.Single().Id, Is.EqualTo(12345));
            Assert.That(_writes.Single().Fields, Is.EquivalentTo(new Dictionary<string, string> { [$"Custom.ReleaseStatusFor{fieldId}"] = "Released" }));
        }

        [Test]
        public async Task AutomaticSdkPr_NoLinkedPlanFallsBackToLegacyLookup()
        {
            _devOps.Setup(s => s.GetReleasePlansBySdkPullRequestAsync(PythonSdkPr, "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _devOps.Setup(s => s.GetReleasePlansForPackageAsync("azure-test", "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var result = await UpdateAsync(releasePlanId: 0, sdkPr: PythonSdkPr);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Message, Does.Contain("No in-progress release plans found"));
            _devOps.Verify(s => s.GetReleasePlansForPackageAsync("azure-test", "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.GetReleasePlansByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            AssertNoWrites();
        }

        [Test]
        public async Task AutomaticSdkPr_AmbiguousLinkDoesNotFallBackToLegacyLookup()
        {
            var other = CreatePlan();
            other.WorkItemId = 22222;
            other.ReleasePlanId = 200;
            _devOps.Setup(s => s.GetReleasePlansBySdkPullRequestAsync(PythonSdkPr, "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([_plan, other]);

            var result = await UpdateAsync(releasePlanId: 0, sdkPr: PythonSdkPr);

            AssertNoWrites();
            Assert.That(result.ResponseError, Does.Contain("exactly one release plan"));
            _devOps.Verify(s => s.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task AutomaticSdkPr_MultipleMatchesRejectBeforePackageFiltering()
        {
            var other = CreatePlan();
            other.WorkItemId = 22222;
            other.ReleasePlanId = 200;
            other.SDKInfo.Single(s => s.Language == "Python").PackageName = "another-package";
            _devOps.Setup(s => s.GetReleasePlansBySdkPullRequestAsync(PythonSdkPr, "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([_plan, other]);

            var result = await UpdateAsync(releasePlanId: 0, sdkPr: PythonSdkPr);

            Assert.That(result.ResponseError, Does.Contain("exactly one").And.Contain("12345").And.Contain("22222"));
            AssertNoWrites();
        }

        [TestCase("id")]
        [TestCase("package")]
        [TestCase("pr")]
        [TestCase("other-language-pr")]
        [TestCase("state")]
        public async Task AutomaticSdkPr_InvalidAdoAssociationDoesNotWrite(string conflict)
        {
            var python = _plan.SDKInfo.Single(s => s.Language == "Python");
            python.SdkPullRequestUrl = PythonSdkPr;
            switch (conflict)
            {
                case "id": _plan.ReleasePlanId = 0; break;
                case "package": python.PackageName = "another-package"; break;
                case "pr": python.SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/200"; break;
                case "other-language-pr": python.SdkPullRequestUrl = ""; _plan.SDKInfo.Single(s => s.Language == "Java").SdkPullRequestUrl = PythonSdkPr; break;
                case "state": _plan.Status = "Finished"; python.ReleaseStatus = "Released"; break;
            }

            var result = await UpdateAsync(releasePlanId: 0, sdkPr: PythonSdkPr);

            Assert.That(result.ResponseError, Is.Not.Null);
            AssertNoWrites();
        }

        [Test]
        public async Task AutomaticSdkPr_DuplicateDisplayIdIsNotReinterpreted()
        {
            _plan.SDKInfo.Single(s => s.Language == "Python").SdkPullRequestUrl = PythonSdkPr;
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(100, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([_plan, CreatePlan()]);

            var result = await UpdateAsync(releasePlanId: 0, sdkPr: PythonSdkPr);

            Assert.That(result.ResponseError, Does.Contain("does not resolve uniquely"));
            AssertNoWrites();
        }

        [Test]
        public async Task ExplicitId_NeverFallsBackToSdkPrLookup()
        {
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(999, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var result = await UpdateAsync(releasePlanId: 999, sdkPr: PythonSdkPr);

            Assert.That(result.ResponseError, Does.Contain("found 0"));
            _devOps.Verify(s => s.GetReleasePlansBySdkPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            AssertNoWrites();
        }

        [TestCase("100")]
        [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/100")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs/pull/100")]
        [TestCase("https://github.com/Other/azure-sdk-for-python/pull/100")]
        [TestCase("https://example.test/Azure/azure-sdk-for-python/pull/100")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/0")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/100/files")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/100?x=1")]
        public async Task AutomaticSdkPr_UnrecognizedUrlUsesLegacyLookup(string sdkPr)
        {
            _devOps.Setup(s => s.GetReleasePlansForPackageAsync("azure-test", "Python", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var result = await UpdateAsync(releasePlanId: 0, sdkPr: sdkPr);

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(_devOps.Invocations.Select(i => i.Method.Name), Is.EqualTo(new[] { nameof(IDevOpsService.GetReleasePlansForPackageAsync) }),
                "An unrecognized PR URL must never be used to query by SDK PR.");
        }

        [Test]
        public async Task Cli_AutomaticSdkPrNeedsNoApiVersion()
        {
            _plan.SpecAPIVersion = string.Empty;
            _plan.SDKInfo.Single(s => s.Language == "Python").SdkPullRequestUrl = PythonSdkPr;
            var command = _tool.GetCommandInstances().First();
            var parse = command.Parse($"--package-name azure-test --language Python --sdk-pull-request {PythonSdkPr}");

            var result = (ReleaseStatusUpdateResponse)await _tool.HandleCommand(parse, CancellationToken.None);

            Assert.That(parse.Errors, Is.Empty);
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanId, Is.EqualTo(100));
            Assert.That(_writes, Has.Count.EqualTo(1));
            Assert.That(command.Parse("--package-name azure-test --language Python --api-version 2026-07-01").Errors, Is.Not.Empty);
        }

        private Task<ReleaseStatusUpdateResponse> UpdateAsync(int releasePlanId = 100,
            string language = "Python", string packageName = "azure-test", string status = "Released", string? version = null,
            string? pipeline = null, string? sdkReleaseType = null, string? sdkPr = null, CancellationToken ct = default)
        {
            return _tool.UpdatePackageReleaseStatus(packageName, language, status, version, releasePlanId,
                sdkReleaseType, pipeline, sdkPr, ct);
        }

        private void AssertNoWrites()
        {
            Assert.That(_devOps.Invocations.Where(i => i.Method.Name == nameof(IDevOpsService.UpdateWorkItemAsync)), Is.Empty);
        }

        private static ReleasePlanWorkItem CreatePlan() => new()
        {
            WorkItemId = 12345,
            ReleasePlanId = 100,
            Revision = 7,
            Status = "In Progress",
            SDKReleaseType = "stable",
            IsManagementPlane = true,
            APISpecProjectPath = "specification/test/project",
            SpecAPIVersion = "2025-01-01",
            SDKInfo =
            [
                new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Pending" },
                new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Pending" },
                new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Pending" },
                new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "Pending" },
                new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "Pending" }
            ]
        };
    }
}
