// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb.Repositories
{
    public class ReviewManager
    {

        private readonly IAuthorizationService _authorizationService;

        private readonly CosmosReviewRepository _reviewsRepository;

        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly BlobOriginalsRepository _originalsRepository;

        private readonly CosmosCommentsRepository _commentsRepository;

        private readonly IEnumerable<LanguageService> _languageServices;

        private readonly NotificationManager _notificationManager;

        private readonly DevopsArtifactRepository _devopsArtifactRepository;

        private readonly PackageNameManager _packageNameManager;

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public ReviewManager(
            IAuthorizationService authorizationService,
            CosmosReviewRepository reviewsRepository,
            BlobCodeFileRepository codeFileRepository,
            BlobOriginalsRepository originalsRepository,
            CosmosCommentsRepository commentsRepository,
            IEnumerable<LanguageService> languageServices,
            NotificationManager notificationManager,
            DevopsArtifactRepository devopsArtifactRepository,
            PackageNameManager packageNameManager)
        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
            _commentsRepository = commentsRepository;
            _languageServices = languageServices;
            _notificationManager = notificationManager;
            _devopsArtifactRepository = devopsArtifactRepository;
            _packageNameManager = packageNameManager;
        }

        public async Task<ReviewModel> CreateReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool runAnalysis)
        {
            ReviewModel review = new ReviewModel
            {
                Author = user.GetGitHubLogin(),
                CreationDate = DateTime.UtcNow,
                RunAnalysis = runAnalysis,
                Name = originalName,
                FilterType = ReviewType.Manual
            };
            await AddRevisionAsync(user, review, originalName, label, fileStream);
            return review;
        }

        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool closed, string language, string packageName = null, ReviewType filterType = ReviewType.Manual)
        {
            return _reviewsRepository.GetReviewsAsync(closed, language, packageName: packageName, filterType: filterType);
        }

        public async Task DeleteReviewAsync(ClaimsPrincipal user, string id)
        {
            var reviewModel = await _reviewsRepository.GetReviewAsync(id);
            await AssertReviewOwnerAsync(user, reviewModel);

            await _reviewsRepository.DeleteReviewAsync(reviewModel);

            foreach (var revision in reviewModel.Revisions)
            {
                foreach (var file in revision.Files)
                {
                    if (file.HasOriginal)
                    {
                        await _originalsRepository.DeleteOriginalAsync(file.ReviewFileId);
                    }

                    await _codeFileRepository.DeleteCodeFileAsync(revision.RevisionId, file.ReviewFileId);
                }
            }

            await _commentsRepository.DeleteCommentsAsync(id);
        }

        public async Task<ReviewModel> GetReviewAsync(ClaimsPrincipal user, string id)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var review = await _reviewsRepository.GetReviewAsync(id);
            review.UpdateAvailable = IsUpdateAvailable(review);

            // Handle old model
#pragma warning disable CS0618 // Type or member is obsolete
            if (review.Revisions.Count == 0 && review.Files.Count == 1)
            {
                var file = review.Files[0];
#pragma warning restore CS0618 // Type or member is obsolete
                review.Revisions.Add(new ReviewRevisionModel()
                {
                    RevisionId = file.ReviewFileId,
                    CreationDate = file.CreationDate,
                    Files =
                    {
                        file
                    }
                });
            }

            if (review.PackageName != null && review.PackageDisplayName == null)
            {
                var p = _packageNameManager.GetPackageDetails(review.PackageName);
                review.PackageDisplayName = p?.DisplayName;
                review.ServiceName = p?.ServiceName;
            }

            return review;
        }

        private async Task UpdateReviewAsync(ReviewModel review)
        {
            foreach (var revision in review.Revisions.Reverse())
            {
                foreach (var file in revision.Files)
                {
                    if (!file.HasOriginal)
                    {
                        continue;
                    }

                    try
                    {
                        var fileOriginal = await _originalsRepository.GetOriginalAsync(file.ReviewFileId);
                        var languageService = GetLanguageService(file.Language);

                        // file.Name property has been repurposed to store package name and version string
                        // This is causing issue when updating review using latest parser since it expects Name field as file name
                        // We have added a new property FileName which is only set for new reviews
                        // All older reviews needs to be handled by checking review name field
                        var fileName = file.FileName ?? (Path.HasExtension(review.Name) ? review.Name : file.Name);
                        var codeFile = await languageService.GetCodeFileAsync(fileName, fileOriginal, review.RunAnalysis);
                        await _codeFileRepository.UpsertCodeFileAsync(revision.RevisionId, file.ReviewFileId, codeFile);
                        // update only version string
                        file.VersionString = codeFile.VersionString;
                    }
                    catch (Exception ex) {
                        _telemetryClient.TrackTrace("Failed to update review " + review.ReviewId);
                        _telemetryClient.TrackException(ex);
                    }                    
                }
            }
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        internal async Task UpdateReviewAsync(ClaimsPrincipal user, string id)
        {
            var review = await GetReviewAsync(user, id);
            await UpdateReviewAsync(review);
        }

        public async Task AddRevisionAsync(
            ClaimsPrincipal user,
            string reviewId,
            string name,
            string label,
            Stream fileStream)
        {
            var review = await GetReviewAsync(user, reviewId);
            await AssertAutomaticReviewModifier(user, review);
            await AddRevisionAsync(user, review, name, label, fileStream);
        }

        private async Task AddRevisionAsync(
            ClaimsPrincipal user,
            ReviewModel review,
            string name,
            string label,
            Stream fileStream)
        {
            var revision = new ReviewRevisionModel();

            ReviewCodeFileModel codeFile = await CreateFileAsync(
                revision.RevisionId,
                name,
                fileStream,
                review.RunAnalysis);

            revision.Files.Add(codeFile);
            revision.Author = user.GetGitHubLogin();
            revision.Label = label;

            review.Revisions.Add(revision);

            if (review.PackageName != null)
            {
                var p = _packageNameManager.GetPackageDetails(review.PackageName);
                review.PackageDisplayName = p?.DisplayName ?? review.PackageDisplayName;
                review.ServiceName = p?.ServiceName ?? review.ServiceName;
            }

            // auto subscribe revision creation user
            await _notificationManager.SubscribeAsync(review, user);

            await _reviewsRepository.UpsertReviewAsync(review);
            await _notificationManager.NotifySubscribersOnNewRevisionAsync(revision, user);
        }

        private async Task<ReviewCodeFileModel> CreateFileAsync(
            string revisionId,
            string originalName,
            Stream fileStream,
            bool runAnalysis)
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFile(originalName, fileStream, runAnalysis, memoryStream);
            var reviewCodeFileModel = await CreateReviewCodeFileModel(revisionId, memoryStream, codeFile);
            reviewCodeFileModel.FileName = originalName;
            return reviewCodeFileModel;
        }

        public async Task<CodeFile> CreateCodeFile(
            string originalName,
            Stream fileStream,
            bool runAnalysis,
            MemoryStream memoryStream)
        {
            var languageService = _languageServices.FirstOrDefault(s => s.IsSupportedFile(originalName));
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            CodeFile codeFile = await languageService.GetCodeFileAsync(
                originalName,
                memoryStream,
                runAnalysis);

            return codeFile;
        }

        public async Task<ReviewCodeFileModel> CreateReviewCodeFileModel(string revisionId, MemoryStream memoryStream, CodeFile codeFile)
        {
            var reviewCodeFileModel = new ReviewCodeFileModel
            {
                HasOriginal = true,
            };

            InitializeFromCodeFile(reviewCodeFileModel, codeFile);
            memoryStream.Position = 0;
            await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.ReviewFileId, memoryStream);
            await _codeFileRepository.UpsertCodeFileAsync(revisionId, reviewCodeFileModel.ReviewFileId, codeFile);

            return reviewCodeFileModel;
        }

        public async Task DeleteRevisionAsync(ClaimsPrincipal user, string id, string revisionId)
        {
            ReviewModel review = await GetReviewAsync(user, id);
            ReviewRevisionModel revision = review.Revisions.Single(r => r.RevisionId == revisionId);
            await AssertRevisionOwner(user, revision);

            if (review.Revisions.Count < 2)
            {
                return;
            }
            review.Revisions.Remove(revision);
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task UpdateRevisionLabelAsync(ClaimsPrincipal user, string id, string revisionId, string label)
        {
            ReviewModel review = await GetReviewAsync(user, id);
            ReviewRevisionModel revision = review.Revisions.Single(r => r.RevisionId == revisionId);
            await AssertRevisionOwner(user, revision);
            revision.Label = label;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task ToggleIsClosedAsync(ClaimsPrincipal user, string id)
        {
            var review = await GetReviewAsync(user, id);
            review.IsClosed = !review.IsClosed;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        private void InitializeFromCodeFile(ReviewCodeFileModel file, CodeFile codeFile)
        {
            file.Language = codeFile.Language;
            file.VersionString = codeFile.VersionString;
            file.Name = codeFile.Name;
            file.PackageName = codeFile.PackageName;
        }

        private LanguageService GetLanguageService(string language)
        {
           return _languageServices.Single(service => service.Name == language);
        }

        private async Task AssertReviewOwnerAsync(ClaimsPrincipal user, ReviewModel reviewModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, reviewModel, new[] { ReviewOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private async Task AssertRevisionOwner(ClaimsPrincipal user, ReviewRevisionModel revisionModel)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                revisionModel,
                new[] { RevisionOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        public async Task ToggleApprovalAsync(ClaimsPrincipal user, string id, string revisionId)
        {
            ReviewModel review = await GetReviewAsync(user, id);
            ReviewRevisionModel revision = review.Revisions.Single(r => r.RevisionId == revisionId);
            await AssertApprover(user, revision);
            var userId = user.GetGitHubLogin();
            if (revision.Approvers.Contains(userId))
            {
                //Revert approval
                revision.Approvers.Remove(userId);
            }
            else
            {
                //Approve revision
                revision.Approvers.Add(userId);
            }
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        private async Task AssertApprover(ClaimsPrincipal user, ReviewRevisionModel revisionModel)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                revisionModel,
                new[] { ApproverRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private bool IsUpdateAvailable(ReviewModel review)
        {
            return review.Revisions
               .SelectMany(r => r.Files)
               .Any(f => f.HasOriginal && GetLanguageService(f.Language).CanUpdate(f.VersionString));
        }

        public async Task<bool> IsReviewSame(ReviewRevisionModel revision, RenderedCodeFile renderedCodeFile)
        {
            //This will compare and check if new code file content is same as revision in parameter
            var lastRevisionFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var lastRevisionTextLines = lastRevisionFile.RenderText(showDocumentation: false, skipDiff: true);
            var fileTextLines = renderedCodeFile.RenderText(showDocumentation: false, skipDiff: true);
            return lastRevisionTextLines.SequenceEqual(fileTextLines);
        }

        public async Task<ReviewRevisionModel> CreateMasterReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool compareAllRevisions)
        {
            //Generate code file from new uploaded package
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFile(originalName, fileStream, false, memoryStream);
            return await CreateMasterReviewAsync(user, codeFile, originalName, label, memoryStream, compareAllRevisions);
        }

        private async Task<ReviewRevisionModel> CreateMasterReviewAsync(ClaimsPrincipal user,  CodeFile codeFile, string originalName, string label, MemoryStream memoryStream, bool compareAllRevisions)
        { 
            var renderedCodeFile = new RenderedCodeFile(codeFile);

            //Get current master review for package and language
            var review = await _reviewsRepository.GetMasterReviewForPackageAsync(codeFile.Language, codeFile.PackageName);
            bool createNewRevision = true;
            ReviewRevisionModel reviewRevision = null;
            if (review != null)
            {
                // Delete pending revisions if it is not in approved state and if it doesn't have any comments before adding new revision
                // This is to keep only one pending revision since last approval or from initial review revision
                var lastRevision = review.Revisions.LastOrDefault();
                var comments = await _commentsRepository.GetCommentsAsync(review.ReviewId);
                while (lastRevision.Approvers.Count == 0 &&
                       review.Revisions.Count > 1 &&
                       !await IsReviewSame(lastRevision, renderedCodeFile) &&
                       !comments.Any(c => lastRevision.RevisionId == c.RevisionId))
                {
                    review.Revisions.Remove(lastRevision);
                    lastRevision = review.Revisions.LastOrDefault();
                }
                // We should compare against only latest revision when calling this API from scheduled CI runs
                // But any manual pipeline run at release time should compare against all approved revisions to ensure hotfix release doesn't have API change
                // If review surface doesn't match with any approved revisions then we will create new revision if it doesn't match pending latest revision
                if (compareAllRevisions)
                {
                    foreach (var approvedRevision in review.Revisions.Where(r => r.IsApproved).Reverse())
                    {
                        if (await IsReviewSame(approvedRevision, renderedCodeFile))
                        {
                            return approvedRevision;
                        }
                    }
                }

                if (await IsReviewSame(lastRevision, renderedCodeFile))
                {
                    reviewRevision = lastRevision;
                    createNewRevision = false;
                }
            }
            else
            {
                // Package and language combination doesn't have automatically created review. Create a new review.
                review = new ReviewModel
                {
                    Author = user.GetGitHubLogin(),
                    CreationDate = DateTime.UtcNow,
                    RunAnalysis = false,
                    Name = originalName,
                    FilterType = ReviewType.Automatic
                };
            }

            // Check if user is authorized to modify automatic review
            await AssertAutomaticReviewModifier(user, review);
            if (createNewRevision)
            {
                // Update or insert review with new revision
                reviewRevision = new ReviewRevisionModel()
                {
                    Author = user.GetGitHubLogin(),
                    Label = label
                };
                var reviewCodeFileModel = await CreateReviewCodeFileModel(reviewRevision.RevisionId, memoryStream, codeFile);
                reviewCodeFileModel.FileName = originalName;
                reviewRevision.Files.Add(reviewCodeFileModel);
                review.Revisions.Add(reviewRevision);
            }
            
            // Check if review can be marked as approved if another review with same surface level is in approved status
            if (review.Revisions.Last().Approvers.Count() == 0)
            {
                var matchingApprovedRevision = await FindMatchingApprovedRevision(review);
                if (matchingApprovedRevision != null)
                {
                    foreach (var approver in matchingApprovedRevision.Approvers)
                    {
                        review.Revisions.Last().Approvers.Add(approver);
                    }
                }
            }
            await _reviewsRepository.UpsertReviewAsync(review);
            return reviewRevision;
        }

        private async Task AssertAutomaticReviewModifier(ClaimsPrincipal user, ReviewModel reviewModel)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                reviewModel,
                new[] { AutoReviewModifierRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private async Task<ReviewRevisionModel> FindMatchingApprovedRevision(ReviewModel review)
        {
            var revisionModel = review.Revisions.LastOrDefault();
            var revisionFile = revisionModel.Files.FirstOrDefault();
            var codeFile = await _codeFileRepository.GetCodeFileAsync(revisionModel);

            // Get manual reviews to check if a matching review is in approved state
            var reviews = await _reviewsRepository.GetReviewsAsync(false, revisionFile.Language, revisionFile.PackageName, ReviewType.Manual);
            var prReviews = await _reviewsRepository.GetReviewsAsync(false, revisionFile.Language, revisionFile.PackageName, ReviewType.PullRequest);
            reviews = reviews.Concat(prReviews);
            foreach (var r in reviews)
            {
                var approvedRevision = r.Revisions.Where(r => r.IsApproved).LastOrDefault();
                if (approvedRevision != null)
                {
                    bool isReviewSame = await IsReviewSame(approvedRevision, codeFile);
                    if (isReviewSame)
                    {
                        return approvedRevision;
                    }
                }
            }
            return null;
        }

        public async void UpdateReviewBackground()
        {
            var reviews = await _reviewsRepository.GetReviewsAsync(false, "All");
            foreach (var review in reviews.Where(r => IsUpdateAvailable(r)))
            {
                var requestTelemetry = new RequestTelemetry { Name = "Updating Review " + review.ReviewId };
                var operation = _telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    await Task.Delay(500);
                    await UpdateReviewAsync(review);
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e);
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }
        }

        public async Task<CodeFile> GetCodeFile(string repoName,
            string buildId,
            string artifactName,
            string packageName,
            string originalFileName,
            string codeFileName,
            MemoryStream originalFileStream,
            string baselineCodeFileName = "",
            MemoryStream baselineStream = null
            )
        {
            Stream stream = null;
            CodeFile codeFile = null;
            if (string.IsNullOrEmpty(codeFileName))
            {
                // backward compatibility until all languages moved to sandboxing of codefile to pipeline
                stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, originalFileName, "file");
                codeFile = await CreateCodeFile(Path.GetFileName(originalFileName), stream, false, originalFileStream);
            }
            else
            {
                stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, packageName, "zip");
                var archive = new ZipArchive(stream);
                foreach (var entry in archive.Entries)
                {
                    var fileName = Path.GetFileName(entry.Name);
                    if (fileName == originalFileName)
                    {
                        await entry.Open().CopyToAsync(originalFileStream);
                    }

                    if (fileName == codeFileName)
                    {
                        codeFile = await CodeFile.DeserializeAsync(entry.Open());
                    }
                    else if (fileName == baselineCodeFileName)
                    {
                        await entry.Open().CopyToAsync(baselineStream);
                    }
                }
            }

            return codeFile;
        }

        public async Task<ReviewRevisionModel> CreateApiReview(
            ClaimsPrincipal user,
            string buildId,
            string artifactName,
            string originalFileName,
            string label,
            string repoName,
            string packageName,
            string codeFileName,
            bool compareAllRevisions
            )
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await GetCodeFile(repoName, buildId, artifactName, packageName, originalFileName, codeFileName, memoryStream);
            return await CreateMasterReviewAsync(user, codeFile, originalFileName, label, memoryStream, compareAllRevisions);
        }

        public async Task<List<ServiceGroupModel>> GetReviewsByServicesAsync(ReviewType filterType)
        {
            SortedDictionary<string, ServiceGroupModel> response = new ();
            var reviews = await _reviewsRepository.GetReviewsAsync(false, "All", filterType: filterType);
            foreach (var review in reviews)
            {
                var packageDisplayName = review.PackageDisplayName ?? "Other";
                var serviceName = review.ServiceName ?? "Other";
                if (!response.ContainsKey(serviceName))
                {
                    response[serviceName] = new ServiceGroupModel()
                    {
                        ServiceName = serviceName
                    };
                }

                var packageDict = response[serviceName].packages;
                if (!packageDict.ContainsKey(packageDisplayName))
                {
                    packageDict[packageDisplayName] = new PackageGroupModel()
                    {
                        PackageDisplayName = packageDisplayName
                    };
                }
                packageDict[packageDisplayName].reviews.Add(new ReviewDisplayModel(review));
            }
            return response.Values.ToList();
        }

        public async Task<IEnumerable<ReviewModel>> GetReviewsAsync(string ServiceName, string PackageName, ReviewType filterType)
        {
            return await _reviewsRepository.GetReviewsAsync(ServiceName, PackageName, filterType);
        }

        public async Task AutoArchiveReviews(int archiveAfterMonths)
        {
            var reviews = await _reviewsRepository.GetReviewsAsync(false, "All", filterType: ReviewType.Manual);
            // Find all inactive reviews
            reviews = reviews.Where(r => r.LastUpdated.AddMonths(archiveAfterMonths) < DateTime.Now);
            foreach (var review in reviews)
            {
                var requestTelemetry = new RequestTelemetry { Name = "Archiving Review " + review.ReviewId };
                var operation = _telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    review.IsClosed = true;
                    await _reviewsRepository.UpsertReviewAsync(review);
                    await Task.Delay(500);
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e);
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }
        }
    }
}
