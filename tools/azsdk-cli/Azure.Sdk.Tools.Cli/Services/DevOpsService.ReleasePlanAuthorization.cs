// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Configuration;
using Microsoft.VisualStudio.Services.Identity;

namespace Azure.Sdk.Tools.Cli.Services;

public partial class DevOpsService
{
    /// <summary>
    /// Uses the work-item connection's authenticated identity and server-managed
    /// Release project administrator group. Never trusts a caller-supplied email,
    /// the notification recipient, or a local administrator allowlist.
    /// </summary>
    public async Task<bool> IsReleasePlanAdminAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var caller = await connection.GetAuthenticatedIdentityAsync(ct);
        if (caller == null || caller.Id == Guid.Empty)
        {
            throw new InvalidOperationException("The authenticated release-plan identity could not be verified.");
        }

        // This SDK overload has no cancellation argument; still stop awaiting it
        // when the caller cancels, before any subsequent identity lookup or write.
        var project = await connection.GetProjectClient(ct)
            .GetProject(Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT).WaitAsync(ct);
        if (project == null || project.Id == Guid.Empty)
        {
            throw new InvalidOperationException("The release-plan administrator group could not be resolved.");
        }

        var identities = connection.GetIdentityClient(ct);
        var groups = await identities.ReadIdentitiesAsync(
            IdentitySearchFilter.AdministratorsGroup,
            project.Id.ToString(),
            options: default,
            queryMembership: QueryMembership.None,
            cancellationToken: ct);
        if (groups == null || groups.Count != 1 || !groups[0].IsContainer
            || !groups[0].IsActive || groups[0].Descriptor == null)
        {
            throw new InvalidOperationException("The release-plan administrator group could not be verified.");
        }

        // ExpandedUp asks ADO to include nested membership, not just direct
        // membership. Compare descriptors, never display names or email aliases.
        var memberships = await identities.ReadIdentitiesAsync(new[] { caller.Id },
            queryMembership: QueryMembership.ExpandedUp, cancellationToken: ct);
        if (memberships == null || memberships.Count != 1 || memberships[0].Id != caller.Id
            || !memberships[0].IsActive || memberships[0].MemberOf == null)
        {
            throw new InvalidOperationException("Release-plan administrator membership could not be verified.");
        }

        return memberships[0].MemberOf.Contains(groups[0].Descriptor);
    }
}