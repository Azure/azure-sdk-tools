using System;
using System.Collections.Generic;
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

    public async Task<APIVersionModel> GetOrCreateVersionAsync(string reviewId, string packageVersion, APIRevisionType apiRevisionType, int? pullRequestNo = null, string sourceBranch = null)
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

        APIVersionModel existing = await _versionsRepository.GetVersionByIdentifierAsync(reviewId, versionIdentifier);
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

    public async Task<APIVersionModel> GetVersionByIdentifierAsync(string reviewId, string versionIdentifier)
    {
        return await _versionsRepository.GetVersionByIdentifierAsync(reviewId, versionIdentifier);
    }

    public async Task SoftDeleteVersionAsync(string versionId, string reviewId, string userName)
    {
        var version = await _versionsRepository.GetVersionAsync(reviewId, versionId);
        if (version == null || version.IsDeleted)
        {
            return;
        }

        version.IsDeleted = true;
        DateTime now = DateTime.UtcNow;
        version.LastUpdated = now;
        version.ChangeHistory.Add(new APIVersionChangeHistoryModel
        {
            ChangeAction = APIVersionChangeAction.Deleted, ChangedBy = userName, ChangedOn = now
        });

        await _versionsRepository.UpsertVersionAsync(version);
    }
}
