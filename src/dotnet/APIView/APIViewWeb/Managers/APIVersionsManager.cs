using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;

namespace APIViewWeb.Managers;

public class APIVersionsManager : IAPIVersionsManager
{
    private readonly ICosmosVersionsRepository _versionsRepository;

    public APIVersionsManager(ICosmosVersionsRepository versionsRepository)
    {
        _versionsRepository = versionsRepository;
    }

    public async Task<APIVersionModel> GetOrCreateVersionAsync(string reviewId, string packageVersion, int? pullRequestNo = null, string sourceBranch = null)
    {
        string versionIdentifier;
        VersionKind kind;

        if (pullRequestNo.HasValue)
        {
            versionIdentifier = $"PR#{pullRequestNo.Value}";
            kind = VersionKind.PullRequest;
        }
        else
        {
            (versionIdentifier, kind) = VersionNormalizationHelper.NormalizeVersion(packageVersion);
        }

        APIVersionModel existing = await _versionsRepository.GetVersionByIdentifierAsync(reviewId, versionIdentifier, kind);
        if (existing != null)
        {
            return existing;
        }

        DateTime now = DateTime.UtcNow;
        var newVersion = new APIVersionModel
        {
            ReviewId = reviewId,
            VersionIdentifier = versionIdentifier,
            Kind = kind,
            PullRequestNumber = pullRequestNo,
            SourceBranch = sourceBranch,
            PrStatus = pullRequestNo.HasValue ? PullRequestStatus.Open : null,
            CreatedOn = now,
            LastUpdated = now,
            ChangeHistory = [new APIVersionChangeHistoryModel { ChangeAction = APIVersionChangeAction.Created, ChangedOn = now }]
        };

        await _versionsRepository.UpsertVersionAsync(newVersion);
        return newVersion;
    }

    public async Task<IEnumerable<APIVersionModel>> GetVersionsForReviewAsync(string reviewId)
    {
        return await _versionsRepository.GetVersionsAsync(reviewId);
    }

    public async Task<APIVersionModel> GetVersionByIdAsync(string reviewId, string versionId)
    {
        return await _versionsRepository.GetVersionAsync(reviewId, versionId);
    }

    public async Task<APIVersionModel> GetVersionByIdentifierAsync(string reviewId, string versionIdentifier, VersionKind? kind = null)
    {
        return await _versionsRepository.GetVersionByIdentifierAsync(reviewId, versionIdentifier, kind);
    }

    public async Task AutoSoftDeleteExpiredVersionsAsync(DateTime now)
    {
        IEnumerable<APIVersionModel> eligible = await _versionsRepository.GetVersionsEligibleForSoftDeleteAsync(now);
        IEnumerable<Task> tasks = eligible.Select(version =>
        {
            version.IsDeleted = true;
            version.LastUpdated = now;
            // TODO (WI-3): Set version.RetainUntil = now + <configured hard-delete grace period>
            // so that AutoHardDeleteExpiredVersionsAsync can pick it up after the retention window expires.
            version.ChangeHistory.Add(new APIVersionChangeHistoryModel
            {
                ChangeAction = APIVersionChangeAction.Deleted, ChangedOn = now
            });
            return _versionsRepository.UpsertVersionAsync(version);
        });
        await Task.WhenAll(tasks);
    }

    public async Task AutoHardDeleteExpiredVersionsAsync(DateTime now)
    {
        IEnumerable<APIVersionModel> eligible = await _versionsRepository.GetVersionsEligibleForHardDeleteAsync(now);
        IEnumerable<Task> tasks = eligible.Select(version => _versionsRepository.DeleteVersionAsync(version.Id, version.ReviewId));
        await Task.WhenAll(tasks);
    }
}
