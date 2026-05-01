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
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners.Rules
{
    [TestFixture]
    public class InvalidOwnerRuleTests
    {
        private Mock<ITeamUserCache> _mockTeamUserCache;
        private UserOrgVisibilityCache _userOrgVisibilityCache;
        private Mock<ICacheValidator> _mockCacheValidator;
        private Mock<IDevOpsService> _mockDevOps;
        private InvalidOwnerRule _rule;

        [SetUp]
        public void Setup()
        {
            _mockTeamUserCache = new Mock<ITeamUserCache>();
            _mockTeamUserCache.Setup(c => c.GetUsersForTeam(It.IsAny<string>())).Returns(new List<string>());
            _userOrgVisibilityCache = new UserOrgVisibilityCache(string.Empty)
            {
                UserOrgVisibilityDict = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase)
            };
            _mockCacheValidator = new Mock<ICacheValidator>();
            _mockCacheValidator
                .Setup(v => v.ThrowIfCacheOlderThan(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDevOps = new Mock<IDevOpsService>();
            _rule = new InvalidOwnerRule(
                _mockTeamUserCache.Object,
                _userOrgVisibilityCache,
                _mockCacheValidator.Object,
                _mockDevOps.Object,
                new TestLogger<InvalidOwnerRule>()
            );
        }

        [Test]
        public async Task Evaluate_ValidOwner_NoViolations()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "validuser" });
            ConfigureOwnerCaches(["validuser"], new Dictionary<string, bool> { ["validuser"] = true });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_WriteUserWithoutPublicAzureMembership_ReturnsSetInvalid()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "privateazureuser" });
            ConfigureOwnerCaches(["privateazureuser"], new Dictionary<string, bool> { ["privateazureuser"] = false });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Detail, Is.EqualTo(InvalidOwnerRule.SetInvalidDetail));
        }

        [Test]
        public async Task Evaluate_ValidOwner_WithInvalidSince_ReturnsClearInvalidViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "recovereduser", InvalidSince = DateTime.UtcNow.AddDays(-7) });
            ConfigureOwnerCaches(["recovereduser"], new Dictionary<string, bool> { ["recovereduser"] = true });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Detail, Is.EqualTo(InvalidOwnerRule.ClearInvalidDetail));
        }

        [Test]
        public async Task Evaluate_InvalidOwner_NoInvalidSince_ReturnsSetInvalid()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "invaliduser" });
            ConfigureOwnerCaches(["validuser"], new Dictionary<string, bool> { ["validuser"] = true });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-OWN-001"));
            Assert.That(violations[0].Detail, Is.EqualTo(InvalidOwnerRule.SetInvalidDetail));
        }

        [Test]
        public async Task Evaluate_InvalidOwner_AlreadyMarkedInvalid_ReturnsDoNothing()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "invaliduser", InvalidSince = DateTime.UtcNow.AddDays(-3) });
            ConfigureOwnerCaches(["validuser"], new Dictionary<string, bool> { ["validuser"] = true });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Detail, Is.EqualTo(InvalidOwnerRule.DoNothingDetail));
        }

        [Test]
        public void Evaluate_EmptyWriteCache_ThrowsException()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "someuser" });
            _userOrgVisibilityCache.UserOrgVisibilityDict =
                new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase) { ["someuser"] = true };

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));
        }

        [Test]
        public void Evaluate_StaleTeamUserCache_ThrowsException()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "someuser" });
            var expectedMessage = $"{DefaultStorageConstants.TeamUserBlobUri} is stale.";
            _mockCacheValidator
                .Setup(v => v.ThrowIfCacheOlderThan(DefaultStorageConstants.TeamUserBlobUri, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(expectedMessage));

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));

            Assert.That(ex!.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Evaluate_EmptyOrgVisibilityCache_ThrowsException()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "someuser" });
            _mockTeamUserCache.Setup(c => c.GetUsersForTeam(It.IsAny<string>())).Returns(["someuser"]);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));
        }

        [Test]
        public void Evaluate_InconsistentCaches_ThrowsException()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "someuser" });
            ConfigureOwnerCaches(["someuser"], new Dictionary<string, bool> { ["otheruser"] = true });

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));
        }

        [Test]
        public async Task Evaluate_SkipsTeamOwners()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/my-team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
            _mockTeamUserCache.Verify(v => v.GetUsersForTeam(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void GetFixes_ExceedsThreshold_WithoutForce_Throws()
        {
            var owners = Enumerable.Range(1, 6).Select(i =>
                new OwnerWorkItem { WorkItemId = i, GitHubAlias = $"invalid{i}" }).ToList();

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    owners.ToDictionary(o => o.WorkItemId),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = true,
                Force = false,
            };

            var violations = owners.Select(o => new AuditViolation
            {
                RuleId = "AUD-OWN-001",
                Description = $"Invalid owner {o.GitHubAlias}",
                WorkItemId = o.WorkItemId,
                Detail = InvalidOwnerRule.SetInvalidDetail,
            }).ToList();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.GetFixes(context, violations, CancellationToken.None));
        }

        [Test]
        public async Task GetFixes_ExceedsThreshold_WithForce_ReturnsFixes()
        {
            var owners = Enumerable.Range(1, 6).Select(i =>
                new OwnerWorkItem { WorkItemId = i, GitHubAlias = $"invalid{i}" }).ToList();

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    owners.ToDictionary(o => o.WorkItemId),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = true,
                Force = true,
            };

            var violations = owners.Select(o => new AuditViolation
            {
                RuleId = "AUD-OWN-001",
                Description = $"Invalid owner {o.GitHubAlias}",
                WorkItemId = o.WorkItemId,
                Detail = InvalidOwnerRule.SetInvalidDetail,
            }).ToList();

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(6));
            Assert.That(fixes[0].Description, Does.Contain("Set Invalid Since"));
        }

        [Test]
        public async Task GetFixes_SetInvalid_CreatesSetFix()
        {
            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "baduser" };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = true,
                Force = false,
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Invalid", WorkItemId = 10, Detail = InvalidOwnerRule.SetInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(1));
            Assert.That(fixes[0].Description, Does.Contain("Set Invalid Since"));
            Assert.That(fixes[0].Description, Does.Contain("baduser"));
        }

        [Test]
        public async Task GetFixes_DoNothing_NoFix()
        {
            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "baduser", InvalidSince = DateTime.UtcNow.AddDays(-5) };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = true,
                Force = false,
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Invalid", WorkItemId = 10, Detail = InvalidOwnerRule.DoNothingDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task GetFixes_ClearInvalid_CreatesClearFix()
        {
            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "recovereduser", InvalidSince = DateTime.UtcNow.AddDays(-7) };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = true,
                Force = false,
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Now valid", WorkItemId = 10, Detail = InvalidOwnerRule.ClearInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(1));
            Assert.That(fixes[0].Description, Does.Contain("Clear Invalid Since"));
            Assert.That(fixes[0].Description, Does.Contain("recovereduser"));
        }

        [Test]
        public async Task ApplyFix_SetInvalid_CallsUpdateWithInvalidSince()
        {
            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "baduser" };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Invalid", WorkItemId = 10, Detail = InvalidOwnerRule.SetInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);
            var result = await fixes[0].Apply(CancellationToken.None);

            Assert.That(result.Success, Is.True);
            _mockDevOps.Verify(d => d.UpdateWorkItemAsync(
                10,
                It.Is<Dictionary<string, string>>(fields =>
                    fields.ContainsKey("Custom.InvalidSince") &&
                    !string.IsNullOrEmpty(fields["Custom.InvalidSince"])),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task ApplyFix_ClearInvalid_CallsUpdateWithEmptyString()
        {
            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "recovereduser", InvalidSince = DateTime.UtcNow.AddDays(-7) };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Now valid", WorkItemId = 10, Detail = InvalidOwnerRule.ClearInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);
            var result = await fixes[0].Apply(CancellationToken.None);

            Assert.That(result.Success, Is.True);
            _mockDevOps.Verify(d => d.UpdateWorkItemAsync(
                10,
                It.Is<Dictionary<string, string>>(fields =>
                    fields.ContainsKey("Custom.InvalidSince") &&
                    fields["Custom.InvalidSince"] == ""),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void ApplyFix_SetInvalid_UpdateThrows_PropagatesException()
        {
            _mockDevOps.Setup(d => d.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("ADO error"));

            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "baduser" };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Invalid", WorkItemId = 10, Detail = InvalidOwnerRule.SetInvalidDetail }
            };

            var fixes = _rule.GetFixes(context, violations, CancellationToken.None).Result;

            Assert.ThrowsAsync<Exception>(async () => await fixes[0].Apply(CancellationToken.None));
        }

        [Test]
        public void GetFixes_InvalidDetail_ThrowsException()
        {
            var owner = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "user" };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = owner },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
                Fix = true,
                Force = false,
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-001", Description = "Invalid", WorkItemId = 10, Detail = "bogus detail" }
            };

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.GetFixes(context, violations, CancellationToken.None));
        }

        private void ConfigureOwnerCaches(IEnumerable<string> writeUsers, Dictionary<string, bool> userOrgVisibility)
        {
            _mockTeamUserCache
                .Setup(c => c.GetUsersForTeam(It.IsAny<string>()))
                .Returns(writeUsers.ToList());
            _userOrgVisibilityCache.UserOrgVisibilityDict =
                new Dictionary<string, bool>(userOrgVisibility, StringComparer.InvariantCultureIgnoreCase);
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
        public void GetFixes_ThrowsNotImplementedException()
        {
            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-002", Description = "Malformed" }
            };

            Assert.ThrowsAsync<NotImplementedException>(
                () => _rule.GetFixes(CreateContext(), violations, CancellationToken.None));
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

        [Test]
        public async Task ApplyFix_Delete_CallsDeleteWorkItemAsync()
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
            var result = await fixes[0].Apply(CancellationToken.None);

            Assert.That(result.Success, Is.True);
            _mockDevOps.Verify(d => d.DeleteWorkItemAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ApplyFix_Delete_Throws_PropagatesException()
        {
            _mockDevOps.Setup(d => d.DeleteWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("ADO delete error"));

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

            Assert.ThrowsAsync<Exception>(async () => await fixes[0].Apply(CancellationToken.None));
        }

        [Test]
        public void GetFixes_ExceedsThreshold_ThrowsWithoutForce()
        {
            var violations = Enumerable.Range(1, 6).Select(i => new AuditViolation
            {
                RuleId = "AUD-STR-001",
                Description = $"zero owners {i}",
                WorkItemId = i,
            }).ToList();

            var context = new AuditContext
            {
                Force = false,
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            Assert.ThrowsAsync<InvalidOperationException>(
                () => _rule.GetFixes(context, violations, CancellationToken.None));
        }

        [Test]
        public async Task GetFixes_ExceedsThreshold_AllowedWithForce()
        {
            var violations = Enumerable.Range(1, 6).Select(i => new AuditViolation
            {
                RuleId = "AUD-STR-001",
                Description = $"zero owners {i}",
                WorkItemId = i,
            }).ToList();

            var context = new AuditContext
            {
                Force = true,
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(6));
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
        public void GetFixes_ThrowsNotImplementedException()
        {
            Assert.ThrowsAsync<NotImplementedException>(
                () => _rule.GetFixes(
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
                ));
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
            mockRule.SetupGet(r => r.Priority).Returns(10);
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

            var response = await helper.RunAudit(false, false, null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(response.Violations, Is.Empty);
                Assert.That(response.FixResults, Is.Empty);
                Assert.That(response.TotalViolations, Is.EqualTo(0));
                Assert.That(response.FixesApplied, Is.EqualTo(0));
                Assert.That(response.FixesFailed, Is.EqualTo(0));
            });
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
            mockRule.SetupGet(r => r.Priority).Returns(10);
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

        [Test]
        public void RunAudit_FixException_PropagatesException()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem>());

            var mockRule = new Mock<IAuditRule>();
            mockRule.SetupGet(r => r.Priority).Returns(10);
            mockRule.SetupGet(r => r.RuleId).Returns("TEST-001");
            mockRule.SetupGet(r => r.Description).Returns("Test rule");
            mockRule.SetupGet(r => r.CanFix).Returns(true);
            mockRule.Setup(r => r.Evaluate(It.IsAny<AuditContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AuditViolation>
                {
                    new() { RuleId = "TEST-001", Description = "test violation" }
                });

            // GetFixes returns one action that throws
            mockRule.Setup(r => r.GetFixes(It.IsAny<AuditContext>(), It.IsAny<List<AuditViolation>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AuditFixAction>
                {
                    new()
                    {
                        RuleId = "TEST-001",
                        Description = "Broken fix",
                        Apply = _ => throw new InvalidOperationException("Something went wrong"),
                    }
                });

            var helper = new CodeownersAuditHelper(
                mockDevOps.Object,
                new[] { mockRule.Object },
                new TestLogger<CodeownersAuditHelper>()
            );

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await helper.RunAudit(true, false, null, CancellationToken.None));
        }

        [Test]
        public async Task RunAudit_SuccessfulFix_TriggersRebuild()
        {
            int fetchCount = 0;
            var mockDevOps = new Mock<IDevOpsService>();
            mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem>())
                .Callback(() => fetchCount++);

            var mockRule = new Mock<IAuditRule>();
            mockRule.SetupGet(r => r.Priority).Returns(10);
            mockRule.SetupGet(r => r.RuleId).Returns("TEST-001");
            mockRule.SetupGet(r => r.Description).Returns("Test rule");
            mockRule.SetupGet(r => r.CanFix).Returns(true);
            mockRule.Setup(r => r.Evaluate(It.IsAny<AuditContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AuditViolation>
                {
                    new() { RuleId = "TEST-001", Description = "test violation" }
                });
            mockRule.Setup(r => r.GetFixes(It.IsAny<AuditContext>(), It.IsAny<List<AuditViolation>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AuditFixAction>
                {
                    new()
                    {
                        RuleId = "TEST-001",
                        Description = "Good fix",
                        Apply = _ => Task.FromResult(new AuditFixResult
                        {
                            RuleId = "TEST-001",
                            Description = "Good fix",
                            Success = true,
                        }),
                    }
                });

            var helper = new CodeownersAuditHelper(
                mockDevOps.Object,
                new[] { mockRule.Object },
                new TestLogger<CodeownersAuditHelper>()
            );

            await helper.RunAudit(true, false, null, CancellationToken.None);

            // Initial fetch (4 work item types) + rebuild after fix (4 more) = 8
            Assert.That(fetchCount, Is.EqualTo(8));
        }

        [Test]
        public async Task RunAudit_OrdersRulesByPriority()
        {
            var mockDevOps = new Mock<IDevOpsService>();
            mockDevOps.Setup(d => d.FetchWorkItemsPagedAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem>());

            var executionOrder = new List<string>();

            var highPriorityRule = new Mock<IAuditRule>();
            highPriorityRule.SetupGet(r => r.Priority).Returns(20);
            highPriorityRule.SetupGet(r => r.RuleId).Returns("TEST-020");
            highPriorityRule.SetupGet(r => r.Description).Returns("Priority 20 rule");
            highPriorityRule.SetupGet(r => r.CanFix).Returns(false);
            highPriorityRule.Setup(r => r.Evaluate(It.IsAny<AuditContext>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("TEST-020"))
                .ReturnsAsync(new List<AuditViolation>());

            var lowPriorityRule = new Mock<IAuditRule>();
            lowPriorityRule.SetupGet(r => r.Priority).Returns(10);
            lowPriorityRule.SetupGet(r => r.RuleId).Returns("TEST-010");
            lowPriorityRule.SetupGet(r => r.Description).Returns("Priority 10 rule");
            lowPriorityRule.SetupGet(r => r.CanFix).Returns(false);
            lowPriorityRule.Setup(r => r.Evaluate(It.IsAny<AuditContext>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("TEST-010"))
                .ReturnsAsync(new List<AuditViolation>());

            var helper = new CodeownersAuditHelper(
                mockDevOps.Object,
                [highPriorityRule.Object, lowPriorityRule.Object],
                new TestLogger<CodeownersAuditHelper>()
            );

            await helper.RunAudit(false, false, null, CancellationToken.None);

            Assert.That(executionOrder, Is.EqualTo(new[] { "TEST-010", "TEST-020" }));
        }
    }

    [TestFixture]
    public class TeamNotWriteRuleTests
    {
        private Mock<ITeamUserCache> _mockTeamUserCache;
        private Mock<ICacheValidator> _mockCacheValidator;
        private Mock<IDevOpsService> _mockDevOps;
        private TeamNotWriteRule _rule;

        [SetUp]
        public void Setup()
        {
            _mockTeamUserCache = new Mock<ITeamUserCache>();
            _mockTeamUserCache.SetupGet(c => c.TeamUserDict)
                .Returns(new Dictionary<string, List<string>>());
            _mockCacheValidator = new Mock<ICacheValidator>();
            _mockCacheValidator
                .Setup(v => v.ThrowIfCacheOlderThan(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockDevOps = new Mock<IDevOpsService>();
            _rule = new TeamNotWriteRule(
                _mockTeamUserCache.Object,
                _mockCacheValidator.Object,
                _mockDevOps.Object
            );
        }

        [Test]
        public async Task Evaluate_SkipsIndividualOwners()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "individual-user" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_SkipsMalformedTeamAliases()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "badorg/team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_TeamInCache_NoViolation()
        {
            _mockTeamUserCache.SetupGet(c => c.TeamUserDict)
                .Returns(new Dictionary<string, List<string>> { ["my-team"] = new() });
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/my-team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void Evaluate_StaleTeamUserCache_ThrowsException()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/my-team" });
            var expectedMessage = $"{DefaultStorageConstants.TeamUserBlobUri} is stale.";
            _mockCacheValidator
                .Setup(v => v.ThrowIfCacheOlderThan(DefaultStorageConstants.TeamUserBlobUri, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(expectedMessage));

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));

            Assert.That(ex!.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public async Task Evaluate_AzureSdkWriteTeamItself_NoViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/azure-sdk-write" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_TeamNotInCache_ReturnsViolation()
        {
            var context = CreateContext(new OwnerWorkItem { WorkItemId = 1, GitHubAlias = "Azure/bad-team" });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].RuleId, Is.EqualTo("AUD-OWN-003"));
            Assert.That(violations[0].Detail, Is.EqualTo(TeamNotWriteRule.SetInvalidDetail));
        }

        [Test]
        public async Task Evaluate_InvalidTeamAlreadyMarked_DoNothing()
        {
            var context = CreateContext(new OwnerWorkItem
            {
                WorkItemId = 1,
                GitHubAlias = "Azure/bad-team",
                InvalidSince = DateTime.UtcNow.AddDays(-5)
            });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Detail, Is.EqualTo(TeamNotWriteRule.DoNothingDetail));
        }

        [Test]
        public async Task Evaluate_PreviouslyInvalidTeamNowValid_ClearInvalid()
        {
            // Team is in the cache (valid) but has InvalidSince set
            _mockTeamUserCache.SetupGet(c => c.TeamUserDict)
                .Returns(new Dictionary<string, List<string>> { ["recovered-team"] = new() });

            var context = CreateContext(new OwnerWorkItem
            {
                WorkItemId = 1,
                GitHubAlias = "Azure/recovered-team",
                InvalidSince = DateTime.UtcNow.AddDays(-10)
            });

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Detail, Is.EqualTo(TeamNotWriteRule.ClearInvalidDetail));
        }

        [Test]
        public async Task GetFixes_SetsInvalidSinceOnNewlyInvalidTeams()
        {
            var invalidTeam = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "Azure/invalid-team" };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = invalidTeam },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Invalid team", WorkItemId = 10, Detail = TeamNotWriteRule.SetInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(1));
            Assert.That(fixes[0].Description, Does.Contain("Set Invalid Since"));
            Assert.That(fixes[0].Description, Does.Contain("Azure/invalid-team"));
        }

        [Test]
        public async Task GetFixes_ClearsInvalidSinceOnRecoveredTeams()
        {
            var recoveredTeam = new OwnerWorkItem
            {
                WorkItemId = 10,
                GitHubAlias = "Azure/recovered-team",
                InvalidSince = DateTime.UtcNow.AddDays(-10)
            };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = recoveredTeam },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Recovered team", WorkItemId = 10, Detail = TeamNotWriteRule.ClearInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Has.Count.EqualTo(1));
            Assert.That(fixes[0].Description, Does.Contain("Clear Invalid Since"));
        }

        [Test]
        public async Task ApplyFix_SetInvalid_CallsUpdateWithInvalidSince()
        {
            var invalidTeam = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "Azure/invalid-team" };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = invalidTeam },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Invalid team", WorkItemId = 10, Detail = TeamNotWriteRule.SetInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);
            var result = await fixes[0].Apply(CancellationToken.None);

            Assert.That(result.Success, Is.True);
            _mockDevOps.Verify(d => d.UpdateWorkItemAsync(
                10,
                It.Is<Dictionary<string, string>>(fields =>
                    fields.ContainsKey("Custom.InvalidSince") &&
                    !string.IsNullOrEmpty(fields["Custom.InvalidSince"])),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task ApplyFix_ClearInvalid_CallsUpdateWithEmptyString()
        {
            var recoveredTeam = new OwnerWorkItem
            {
                WorkItemId = 10,
                GitHubAlias = "Azure/recovered-team",
                InvalidSince = DateTime.UtcNow.AddDays(-10)
            };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = recoveredTeam },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Recovered team", WorkItemId = 10, Detail = TeamNotWriteRule.ClearInvalidDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);
            var result = await fixes[0].Apply(CancellationToken.None);

            Assert.That(result.Success, Is.True);
            _mockDevOps.Verify(d => d.UpdateWorkItemAsync(
                10,
                It.Is<Dictionary<string, string>>(fields =>
                    fields.ContainsKey("Custom.InvalidSince") &&
                    fields["Custom.InvalidSince"] == ""),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void ApplyFix_SetInvalid_UpdateThrows_PropagatesException()
        {
            _mockDevOps.Setup(d => d.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("ADO error"));

            var invalidTeam = new OwnerWorkItem { WorkItemId = 10, GitHubAlias = "Azure/invalid-team" };
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = invalidTeam },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Invalid team", WorkItemId = 10, Detail = TeamNotWriteRule.SetInvalidDetail }
            };

            var fixes = _rule.GetFixes(context, violations, CancellationToken.None).Result;

            Assert.ThrowsAsync<Exception>(async () => await fixes[0].Apply(CancellationToken.None));
        }

        [Test]
        public async Task GetFixes_DoNothingViolations_ProduceNoFixes()
        {
            var markedTeam = new OwnerWorkItem
            {
                WorkItemId = 10,
                GitHubAlias = "Azure/already-marked",
                InvalidSince = DateTime.UtcNow.AddDays(-5)
            };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem> { [10] = markedTeam },
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Already marked", WorkItemId = 10, Detail = TeamNotWriteRule.DoNothingDetail }
            };

            var fixes = await _rule.GetFixes(context, violations, CancellationToken.None);

            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public void GetFixes_InvalidDetail_Throws()
        {
            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-OWN-003", Description = "Bad detail", WorkItemId = 10, Detail = "Unexpected value" }
            };

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.GetFixes(context, violations, CancellationToken.None));
        }

        [Test]
        public void GetFixes_ExceedsThreshold_ThrowsWithoutForce()
        {
            var owners = Enumerable.Range(1, 6).ToDictionary(
                i => i,
                i => new OwnerWorkItem { WorkItemId = i, GitHubAlias = $"Azure/team-{i}" }
            );

            var context = new AuditContext
            {
                Force = false,
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    owners,
                    new Dictionary<int, LabelWorkItem>(),
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = Enumerable.Range(1, 6).Select(i => new AuditViolation
            {
                RuleId = "AUD-OWN-003", Description = $"Team {i}", WorkItemId = i, Detail = TeamNotWriteRule.SetInvalidDetail
            }).ToList();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.GetFixes(context, violations, CancellationToken.None));
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
    public class LabelNotInRepoLabelsRuleTests
    {
        private RepoLabelCache _repoLabelCache;
        private Mock<ICacheValidator> _mockCacheValidator;
        private LabelNotInRepoLabelsRule _rule;

        [SetUp]
        public void Setup()
        {
            _repoLabelCache = new RepoLabelCache(string.Empty)
            {
                RepoLabelDict = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase)
            };
            _mockCacheValidator = new Mock<ICacheValidator>();
            _mockCacheValidator
                .Setup(v => v.ThrowIfCacheOlderThan(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _rule = new LabelNotInRepoLabelsRule(_repoLabelCache, _mockCacheValidator.Object);
        }

        [Test]
        public async Task Evaluate_LabelExistsInAllRepos_NoViolation()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Storage" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo.Labels.Add(label);

            _repoLabelCache.RepoLabelDict["Azure/azure-sdk-for-net"] =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Storage" };

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
        public async Task Evaluate_LabelMissingFromOneRepo_ReturnsViolation()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Storage" };
            var lo1 = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo1.Labels.Add(label);
            var lo2 = new LabelOwnerWorkItem { WorkItemId = 11, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-java" };
            lo2.Labels.Add(label);

            _repoLabelCache.RepoLabelDict["Azure/azure-sdk-for-net"] =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Storage" };
            _repoLabelCache.RepoLabelDict["Azure/azure-sdk-for-java"] =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Other" };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem> { lo1, lo2 }
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Description, Does.Contain("azure-sdk-for-java"));
            Assert.That(violations[0].Description, Does.Not.Contain("azure-sdk-for-net"));
        }

        [Test]
        public async Task Evaluate_LabelNotReferencedByAny_Skipped()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Orphan" };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public async Task Evaluate_LabelReferencedByPackage_UsesSharedLanguageRepoMapping()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Storage" };
            var package = new PackageWorkItem { WorkItemId = 20, Language = "Rust", PackageName = "pkg" };
            package.Labels.Add(label);

            _repoLabelCache.RepoLabelDict["Azure/azure-sdk-for-rust"] =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Storage" };

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem> { [20] = package },
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem>()
                ),
            };

            var violations = await _rule.Evaluate(context, CancellationToken.None);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void Evaluate_StaleRepoLabelCache_ThrowsException()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Storage" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/azure-sdk-for-net" };
            lo.Labels.Add(label);

            var expectedMessage = $"{DefaultStorageConstants.RepoLabelBlobStorageURI} is stale.";
            _mockCacheValidator
                .Setup(v => v.ThrowIfCacheOlderThan(DefaultStorageConstants.RepoLabelBlobStorageURI, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(expectedMessage));

            var context = new AuditContext
            {
                WorkItemData = new WorkItemData(
                    new Dictionary<int, PackageWorkItem>(),
                    new Dictionary<int, OwnerWorkItem>(),
                    new Dictionary<int, LabelWorkItem> { [1] = label },
                    new List<LabelOwnerWorkItem> { lo }
                ),
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));

            Assert.That(ex!.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Evaluate_RepoMissingFromCache_ThrowsException()
        {
            var label = new LabelWorkItem { WorkItemId = 1, LabelName = "Storage" };
            var lo = new LabelOwnerWorkItem { WorkItemId = 10, LabelType = "Service Owner", Repository = "Azure/missing-repo" };
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

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _rule.Evaluate(context, CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("Azure/missing-repo"));
            Assert.That(ex.Message, Does.Contain(DefaultStorageConstants.RepoLabelBlobStorageURI));
        }

        [Test]
        public void GetFixes_ThrowsNotImplementedException()
        {
            var violations = new List<AuditViolation>
            {
                new() { RuleId = "AUD-LBL-001", Description = "Missing label" }
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

            Assert.ThrowsAsync<NotImplementedException>(
                () => _rule.GetFixes(context, violations, CancellationToken.None));
        }
    }
}
