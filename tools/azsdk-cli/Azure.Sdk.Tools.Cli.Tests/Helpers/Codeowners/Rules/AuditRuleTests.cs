using Moq;
using NUnit.Framework;

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.CodeownersUtils.Caches;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners.Rules
{
    [TestFixture]
    public class InvalidOwnerRuleTests
    {
        private Mock<ICodeownersValidatorHelper> _mockValidator;
        private Mock<IDevOpsService> _mockDevOps;
        private InvalidOwnerRule _rule;

        [SetUp]
        public void Setup()
        {
            _mockValidator = new Mock<ICodeownersValidatorHelper>();
            _mockDevOps = new Mock<IDevOpsService>();
            _rule = new InvalidOwnerRule(
                _mockValidator.Object,
                _mockDevOps.Object,
                new TestLogger<InvalidOwnerRule>()
            );
        }

        [Test]
        public async Task Evaluate_ValidOwner_NoViolations()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "validuser" });
            _mockValidator.Setup(v => v.ValidateCodeOwnerAsync("validuser", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeownersValidationResult { Status = "Success", IsValidCodeOwner = true });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_InvalidOwner_ReturnsViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "invaliduser" });
            _mockValidator.Setup(v => v.ValidateCodeOwnerAsync("invaliduser", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeownersValidationResult { Status = "Success", IsValidCodeOwner = false, Message = "Not a member" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-OWN-001"));
            Assert.That(violations[0].WorkItemId, Is.EqualTo(1));
        }

        [Test]
        public async Task Evaluate_NotFoundOwner_ReturnsViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "ghostuser" });
            _mockValidator.Setup(v => v.ValidateCodeOwnerAsync("ghostuser", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeownersValidationResult { Status = "Error", Message = "GitHub user not found 'ghostuser' not found" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Description, Does.Contain("not found"));
        }

        [Test]
        public void Evaluate_RateLimitError_ThrowsException()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "someuser" });
            _mockValidator.Setup(v => v.ValidateCodeOwnerAsync("someuser", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeownersValidationResult { Status = "Error", Message = "Rate limit exceeded" });

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));
        }

        [Test]
        public async Task Evaluate_SkipsTeamOwners()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/my-team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
            _mockValidator.Verify(v => v.ValidateCodeOwnerAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void GetFixes_ExceedsThreshold_WithoutForce_Throws()
        {
            var context = CreateContext();
            context.Fix = true;
            context.Force = false;

            var violations = Enumerable.Range(1, 6).Select(i => new AuditViolation
            {
                RuleId = "AUD-OWN-001",
                Description = $"Invalid owner {i}",
                WorkItemId = i,
            }).ToList();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.GetFixes(context, violations, CancellationToken.None));
        }

        [Test]
        public async Task GetFixes_ExceedsThreshold_WithForce_ReturnsFixes()
        {
            var owner = new OwnerWorkItem { WorkItemId = 100, GitHubAlias = "invalid1" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 200, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo.Owners.Add(owner);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [100] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem> { lo }
                ),
                Fix = true,
                Force = true,
            };

            var violations = Enumerable.Range(1, 6).Select(i => new AuditViolation
            {
                RuleId = "AUD-OWN-001",
                Description = $"Invalid owner {i}",
                WorkItemId = 100,
            }).ToList();

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(1));
            Assert.That(fixes[0].Description, Does.Contain("Label Owner 200"));
        }

        [Test]
        public async Task GetFixes_RemovesRelationsFromLabelOwnersAndPackages()
        {
            var invalidOwner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "baduser" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 20, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo.Owners.Add(invalidOwner);

            var pkg = new PackageWorkItem { WorkItemId = 30, PackageName = "azure-test", Language = "dotnet" };
            pkg.Owners.Add(invalidOwner);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem> { [30] = pkg },
                    new Dictionary<int, OwnerWorkItem> { [10] = invalidOwner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem> { lo }
                ),
                Fix = true,
                Force = false,
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Invalid", WorkItemId = 10 }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(2));
            Assert.That(fixes[0].Description, Does.Contain("Label Owner 20"));
            Assert.That(fixes[1].Description, Does.Contain("Package 30"));
        }

        private static AuditContext CreateContext(params OwnerWorkItem[] owners)
        {
            var ownerDict = owners.ToDictionary(o => o.WorkItemId);
            return new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    ownerDict,
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = false,
                Force = false,
            };
        }
    }

    [TestFixture]
    public class MalformedTeamRuleTests
    {
        private MalformedTeamRule _rule;

        [SetUp]
        public void Setup()
        {
            _rule = new MalformedTeamRule();
        }

        [Test]
        public async Task Evaluate_WellFormedTeam_NoViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/my-team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_MalformedTeam_ReturnsViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "badorg/my-team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-OWN-002"));
        }

        [Test]
        public async Task Evaluate_IndividualOwner_Ignored()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "individual-user" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_EmptyTeamSlug_ReturnsViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetFixes_ReturnsEmpty()
        {
            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-002", Description = "Malformed" }
            };

            var fixes = await _rule.GetFixes(CreateContext(), violations, CancellationToken.None);

            Assert.That(fixes, Is.Empty);
        }

        private static AuditContext CreateContext(params OwnerWorkItem[] owners)
        {
            var ownerDict = owners.ToDictionary(o => o.WorkItemId);
            return new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    ownerDict,
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };
        }
    }

    [TestFixture]
    public class LabelOwnerMissingOwnersRuleTests
    {
        private Mock<IDevOpsService> _mockDevOps;
        private LabelOwnerMissingOwnersRule _rule;

        [SetUp]
        public void Setup()
        {
            _mockDevOps = new Mock<IDevOpsService>();
            _rule = new LabelOwnerMissingOwnersRule(
                _mockDevOps.Object,
                new TestLogger<LabelOwnerMissingOwnersRule>()
            );
        }

        [Test]
        public async Task Evaluate_LabelOwnerWithOwners_NoViolation()
        {
            var owner = new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "user1" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo.Owners.Add(owner);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [1] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_LabelOwnerWithZeroOwners_ReturnsViolation()
        {
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-STR-001"));
            Assert.That(violations[0].Description, Does.Contain("zero owners"));
        }

        [Test]
        public async Task GetFixes_ReturnsDeleteAction()
        {
            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-STR-001", Description = "zero owners", WorkItemId = 10 }
            };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(1));
            Assert.That(fixes[0].Description, Does.Contain("Delete"));
        }
    }

    [TestFixture]
    public class LabelOwnerMissingLabelsRuleTests
    {
        private LabelOwnerMissingLabelsRule _rule;

        [SetUp]
        public void Setup()
        {
            _rule = new LabelOwnerMissingLabelsRule();
        }

        [Test]
        public async Task Evaluate_LabelOwnerWithLabels_NoViolation()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "test-label" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo.Labels.Add(label);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_LabelOwnerWithZeroLabels_ReturnsViolation()
        {
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-STR-002"));
        }

        [Test]
        public async Task GetFixes_ReturnsEmpty()
        {
            var fixes = await _rule.GetFixes(
                new AuditContext
                {
                    WorkItemData = new WorkItemData(
                        new Dictionary<int, PackageWorkItem>(),
                        new Dictionary<int, OwnerWorkItem>(),
                        new Dictionary<int, LabelWorkItem>(),
                        new List<LabelOwnerWorkItem>()
                    ),
                },
                new List<AuditViolation>(),
                CancellationToken.None
            );

            Assert.That(fixes, Is.Empty);
        }
    }

    [TestFixture]
    public class ServiceAttentionMisuseRuleTests
    {
        private ServiceAttentionMisuseRule _rule;

        [SetUp]
        public void Setup()
        {
            _rule = new ServiceAttentionMisuseRule();
        }

        [Test]
        public async Task Evaluate_PRLabelWithServiceAttention_ReturnsViolation()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Service Attention" };
            var lo = new LabelOwnerWorkItem
            {
                WorkItemId = 10,
                LabelType = "PR Label",
                Repository = "Azure/azure-sdk-for-net",
            };
            lo.Labels.Add(label);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-LBL-002"));
            Assert.That(violations[0].Description, Does.Contain("PR Label"));
        }

        [Test]
        public async Task Evaluate_ServiceOwnerOnlyServiceAttention_ReturnsViolation()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Service Attention" };
            var lo = new LabelOwnerWorkItem
            {
                WorkItemId = 10,
                LabelType = "Service Owner",
                Repository = "Azure/azure-sdk-for-net",
            };
            lo.Labels.Add(label);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Description, Does.Contain("only label"));
        }

        [Test]
        public async Task Evaluate_ServiceOwnerWithMultipleLabels_NoViolation()
        {
            var label1 = new LabelWorkItem { WorkItemId = 1, LabelName = "Service Attention" };
            var label2 = new LabelWorkItem { WorkItemId = 2, LabelName = "Storage" };
            var lo = new LabelOwnerWorkItem
            {
                WorkItemId = 10,
                LabelType = "Service Owner",
                Repository = "Azure/azure-sdk-for-net",
            };
            lo.Labels.Add(label1);
            lo.Labels.Add(label2);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label1, [2] = label2 },
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_PackageWithServiceAttention_ReturnsViolation()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Service Attention" };
            var pkg = new PackageWorkItem { WorkItemId = 20, PackageName = "azure-test", Language = "dotnet" };
            pkg.Labels.Add(label);

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem> { [20] = pkg },
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Description, Does.Contain("Package 20"));
        }
    }

    [TestFixture]
    public class CodeownersAuditHelperTests
    {
        [Test]
        public async Task RunAudit_NoViolations_ReturnsEmptyReport()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            var mockRule = new Mock<IAuditRule>();
            mockRule.SetupGet(r => r.RuleId).Returns("TEST-001");
            mockRule.SetupGet(r => r.Description).Returns("Test rule");
            mockRule.SetupGet(r => r.CanFix).Returns(false);
            mockRule.Setup(r => r.Evaluate(It.IsAny<AuditContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AuditViolation>());

            mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem>());

            var helper = new CodeownersAuditHelper(
                mockDevOps.Object,
                new[] { mockRule.Object },
                new TestLogger<CodeownersAuditHelper>()
            );

            var report = await helper.RunAudit(false, false, null, CancellationToken.None);

            Assert.That(report.Violations, Is.Empty);
            Assert.That(report.FixesApplied, Is.Empty);
        }

        [Test]
        public async Task RunAudit_RepoFilter_PassesFilterToContext()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem>());

            AuditContext? capturedContext = null;
            var mockRule = new Mock<IAuditRule>();
            mockRule.SetupGet(r => r.RuleId).Returns("TEST-001");
            mockRule.SetupGet(r => r.Description).Returns("Test rule");
            mockRule.SetupGet(r => r.CanFix).Returns(false);
            mockRule.Setup(r => r.Evaluate(It.IsAny<AuditContext>(), It.IsAny<CancellationToken>()))
                .Callback<AuditContext, CancellationToken>((ctx, _) => capturedContext = ctx)
                .ReturnsAsync(new List<AuditViolation>());

            var helper = new CodeownersAuditHelper(
                mockDevOps.Object,
                new[] { mockRule.Object },
                new TestLogger<CodeownersAuditHelper>()
            );

            await helper.RunAudit(true, true, "Azure/azure-sdk-for-net", CancellationToken.None);

            Assert.That(capturedContext, Is.Not.Null);
            Assert.That(capturedContext!.Fix, Is.True);
            Assert.That(capturedContext.Force, Is.True);
            Assert.That(capturedContext.Repo, Is.EqualTo("Azure/azure-sdk-for-net"));
        }
    }
}
