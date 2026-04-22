// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-LBL-002: Detect misuse of "Service Attention" label.
/// Flags:
/// - Label Owner of type "PR Label" with Service Attention in its labels.
/// - Label Owner of type "Service Owner" with ONLY Service Attention as its label.
/// - Package with Service Attention as a PR label.
/// Report only.
/// </summary>
public class ServiceAttentionMisuseRule : IAuditRule
{
    private const string ServiceAttentionLabel = "Service Attention";

    public int Priority => 50;
    public string RuleId => "AUD-LBL-002";
    public string Description => "Service Attention misused as PR label or sole service label";
    public bool CanFix => false;

    public Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();

        foreach (var lo in context.WorkItemData.LabelOwners)
        {
            var hasServiceAttention = lo.Labels.Any(l =>
                l.LabelName.Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase));

            if (!hasServiceAttention)
            {
                continue;
            }

            if (lo.LabelType.Equals("PR Label", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Label Owner {lo.WorkItemId} (PR Label): has 'Service Attention' as a label",
                    WorkItemId = lo.WorkItemId,
                    Detail = $"Repository: {lo.Repository}, Path: {lo.RepoPath}",
                });
            }
            else if (lo.LabelType.Equals("Service Owner", StringComparison.OrdinalIgnoreCase) ||
                     lo.LabelType.Equals("Azure SDK Owner", StringComparison.OrdinalIgnoreCase))
            {
                if (lo.Labels.Count == 1)
                {
                    violations.Add(new AuditViolation
                    {
                        RuleId = RuleId,
                        Description = $"Label Owner {lo.WorkItemId} ({lo.LabelType}): only label is 'Service Attention'",
                        WorkItemId = lo.WorkItemId,
                        Detail = $"Repository: {lo.Repository}, Path: {lo.RepoPath}",
                    });
                }
            }
        }

        // Check Packages
        foreach (var pkg in context.WorkItemData.Packages.Values)
        {
            var hasServiceAttention = pkg.Labels.Any(l =>
                l.LabelName.Equals(ServiceAttentionLabel, StringComparison.OrdinalIgnoreCase));

            if (hasServiceAttention)
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Package {pkg.WorkItemId} ({pkg.PackageName}): has 'Service Attention' as a label",
                    WorkItemId = pkg.WorkItemId,
                    Detail = $"Language: {pkg.Language}",
                });
            }
        }

        return Task.FromResult(violations);
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        throw new NotImplementedException($"{RuleId} is report-only and does not support fixes.");
    }
}
