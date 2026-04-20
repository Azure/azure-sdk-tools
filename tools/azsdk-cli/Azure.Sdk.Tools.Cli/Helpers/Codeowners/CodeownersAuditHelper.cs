// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

public class CodeownersAuditHelper(
    IDevOpsService devOpsService,
    IEnumerable<IAuditRule> rules,
    ILogger<CodeownersAuditHelper> logger
) : ICodeownersAuditHelper
{
    public async Task<AuditReport> RunAudit(bool fix, bool force, string? repo, CancellationToken ct)
    {
        var context = new AuditContext
        {
            WorkItemData = await FetchAllWorkItems(repo, ct),
            Fix = fix,
            Force = force,
            Repo = repo,
        };

        var report = new AuditReport();

        // Rules are injected in the order they are registered in DI.
        // Execution order:
        // 1. AUD-OWN-001 — no dependencies
        // 2. AUD-OWN-002 — no dependencies
        // 3. AUD-OWN-003 — skips malformed aliases internally (OWN-002 is report-only)
        // 4. AUD-LBL-001 — no dependencies on owner rules
        // 5. AUD-LBL-002 — no dependencies on owner rules
        // 6. AUD-STR-001 — depends on OWN-001, OWN-003 (owner removals may orphan Label Owners)
        // 7. AUD-STR-002 — depends on LBL-001, LBL-002 (report only, no fix)
        foreach (var rule in rules)
        {
            logger.LogInformation("Evaluating rule {RuleId}: {Description}", rule.RuleId, rule.Description);
            var violations = await rule.Evaluate(context, ct);

            report.Violations.AddRange(violations);
            logger.LogInformation("Rule {RuleId}: {Count} violation(s) found", rule.RuleId, violations.Count);

            if (violations.Count > 0 && fix && rule.CanFix)
            {
                var fixes = await rule.GetFixes(context, violations, ct);
                logger.LogInformation("Rule {RuleId}: applying {Count} fix(es)", rule.RuleId, fixes.Count);

                bool anyApplied = false;
                foreach (var fixAction in fixes)
                {
                    var result = await fixAction.Apply(ct);
                    report.FixesApplied.Add(result);

                    if (result.Success || result.AlreadyApplied)
                    {
                        anyApplied = true;
                        logger.LogInformation("  Fixed: {Description} (AlreadyApplied={AlreadyApplied})",
                            result.Description, result.AlreadyApplied);
                    }
                    else
                    {
                        logger.LogError("  Fix failed: {Description} — {Error}", result.Description, result.ErrorMessage);
                    }
                }

                // Full rebuild of context after any fixes were applied
                if (anyApplied)
                {
                    logger.LogInformation("Rebuilding work item context after fixes from {RuleId}", rule.RuleId);
                    context.WorkItemData = await FetchAllWorkItems(repo, ct);
                }
            }
        }

        return report;
    }

    internal async Task<WorkItemData> FetchAllWorkItems(string? repo, CancellationToken ct)
    {
        // Owners — always global
        var owners = await FetchWorkItems(
            "[System.WorkItemType] = 'Owner'",
            WorkItemMappers.MapToOwnerWorkItem, ct);
        logger.LogInformation("Fetched {Count} Owner work items", owners.Count);

        // Labels — always global
        var labels = await FetchWorkItems(
            "[System.WorkItemType] = 'Label'",
            WorkItemMappers.MapToLabelWorkItem, ct);
        logger.LogInformation("Fetched {Count} Label work items", labels.Count);

        // Packages — filtered by language if --repo is specified
        string packageWhere = "[System.WorkItemType] = 'Package'";
        if (!string.IsNullOrEmpty(repo))
        {
            var languageString = CodeownersManagementHelper.RepoToLanguageString(repo);
            var escapedLanguage = languageString.Replace("'", "''");
            packageWhere += $" AND [Custom.Language] = '{escapedLanguage}'";
        }
        var packages = await FetchWorkItems(packageWhere, WorkItemMappers.MapToPackageWorkItem, ct);
        packages = WorkItemMappers.GetLatestPackageVersions(packages);
        logger.LogInformation("Fetched {Count} Package work items", packages.Count);

        // Label Owners — filtered by Custom.Repository if --repo is specified
        string labelOwnerWhere = "[System.WorkItemType] = 'Label Owner'";
        if (!string.IsNullOrEmpty(repo))
        {
            var escapedRepo = repo.Replace("'", "''");
            labelOwnerWhere += $" AND [Custom.Repository] = '{escapedRepo}'";
        }
        var labelOwners = await FetchWorkItems(labelOwnerWhere, WorkItemMappers.MapToLabelOwnerWorkItem, ct);
        logger.LogInformation("Fetched {Count} Label Owner work items", labelOwners.Count);

        var data = new WorkItemData(
            packages.ToDictionary(p => p.WorkItemId),
            owners.ToDictionary(o => o.WorkItemId),
            labels.ToDictionary(l => l.WorkItemId),
            labelOwners);

        data.HydrateRelationships();
        return data;
    }

    private async Task<List<T>> FetchWorkItems<T>(
        string whereClause,
        Func<WorkItem, T> factory,
        CancellationToken ct)
    {
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND {whereClause}";
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations, ct: ct);
        return workItems.Select(factory).ToList();
    }
}
