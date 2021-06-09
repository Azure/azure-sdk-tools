// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb.Respositories
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

        public ReviewManager(
            IAuthorizationService authorizationService,
            CosmosReviewRepository reviewsRepository,
            BlobCodeFileRepository codeFileRepository,
            BlobOriginalsRepository originalsRepository,
            CosmosCommentsRepository commentsRepository,
            IEnumerable<LanguageService> languageServices,
            NotificationManager notificationManager)
        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
            _commentsRepository = commentsRepository;
            _languageServices = languageServices;
            _notificationManager = notificationManager;
        }

        public async Task<ReviewModel> CreateReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool runAnalysis)
        {
            ReviewModel review = new ReviewModel
            {
                Author = user.GetGitHubLogin(),
                CreationDate = DateTime.UtcNow,
                RunAnalysis = runAnalysis,
                Name = originalName
            };
            await AddRevisionAsync(user, review, originalName, label, fileStream);
            return review;
        }

        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool closed, string language, string packageName = null, bool? automatic = null)
        {
            return _reviewsRepository.GetReviewsAsync(closed, language, packageName: packageName, isAutomatic: automatic);
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
            return review;
        }

        private async Task UpdateReviewAsync(ReviewModel review)
        {
            foreach (var revision in review.Revisions)
            {
                foreach (var file in revision.Files)
                {
                    if (!file.HasOriginal)
                    {
                        continue;
                    }

                    var fileOriginal = await _originalsRepository.GetOriginalAsync(file.ReviewFileId);
                    var languageService = GetLanguageService(file.Language);

                    // file.Name property has been repurposed to store package name and version string
                    // This is causing issue when updating review using latest parser since it expects Name field as file name
                    // We have added a new property FileName which is only set for new reviews
                    // All older reviews needs to be handled by checking Name field
                    // If name field has no extension and File Name is Emtpy then use review.Name
                    var fileName = file.FileName ?? (Path.HasExtension(file.Name)? file.Name : review.Name);
                    var codeFile = await languageService.GetCodeFileAsync(fileName, fileOriginal, review.RunAnalysis);
                    await _codeFileRepository.UpsertCodeFileAsync(revision.RevisionId, file.ReviewFileId, codeFile);
                    InitializeFromCodeFile(file, codeFile);
                    file.FileName = fileName;
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

        private async Task<CodeFile> CreateCodeFile(
            string originalName,
            Stream fileStream,
            bool runAnalysis,
            MemoryStream memoryStream)
        {
            var languageService = _languageServices.Single(s => s.IsSupportedFile(originalName));
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            CodeFile codeFile = await languageService.GetCodeFileAsync(
                originalName,
                memoryStream,
                runAnalysis);

            return codeFile;
        }

        private async Task<ReviewCodeFileModel> CreateReviewCodeFileModel(string revisionId, MemoryStream memoryStream, CodeFile codeFile)
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
            await AssertReviewOwnerAsync(user, review);

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

        private async Task<bool> IsReviewSame(ReviewRevisionModel revision, RenderedCodeFile renderedCodeFile)
        {
            //This will compare and check if new code file content is same as revision in parameter
            var lastRevisionFile = await _codeFileRepository.GetCodeFileAsync(revision);
            var lastRevisionTextLines = lastRevisionFile.RenderText(showDocumentation: false, skipDiff: true);
            var fileTextLines = renderedCodeFile.RenderText(showDocumentation: false, skipDiff: true);
            return lastRevisionTextLines.SequenceEqual(fileTextLines);
        }

        public async Task<ReviewModel> CreateMasterReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool runAnalysis)
        {
            //Generate code file from new uploaded package
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFile(originalName, fileStream, runAnalysis, memoryStream);

            //Get current master review for package and language
            var review = await _reviewsRepository.GetMasterReviewForPackageAsync(codeFile.Language, codeFile.PackageName);
            bool createNewRevision = true;
            if (review != null)
            {
                // Delete pending revisions if it is not in approved state before adding new revision
                // This is to keep only one pending revision since last approval or from initial review revision
                var lastRevision = review.Revisions.LastOrDefault();
                while (lastRevision.Approvers.Count == 0 && review.Revisions.Count > 1)
                {
                    review.Revisions.Remove(lastRevision);
                    lastRevision = review.Revisions.LastOrDefault();
                }

                var renderedCodeFile = new RenderedCodeFile(codeFile);
                var noDiffFound = await IsReviewSame(review.Revisions.LastOrDefault(), renderedCodeFile);
                if (noDiffFound)
                {
                    // No change is detected from last revision so no need to update this revision
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
                    RunAnalysis = runAnalysis,
                    Name = originalName,
                    IsAutomatic = true
                };
            }

            // Check if user is authorized to modify automatic review
            await AssertAutomaticReviewModifier(user, review);
            if (createNewRevision)
            {
                // Update or insert review with new revision
                var revision = new ReviewRevisionModel()
                {
                    Author = user.GetGitHubLogin(),
                    Label = label
                };
                var reviewCodeFileModel = await CreateReviewCodeFileModel(revision.RevisionId, memoryStream, codeFile);
                reviewCodeFileModel.FileName = originalName;
                revision.Files.Add(reviewCodeFileModel);
                review.Revisions.Add(revision);
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
            return review;
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
            var reviews = await _reviewsRepository.GetReviewsAsync(false, revisionFile.Language, revisionFile.PackageName, false);
            foreach (var r in reviews)
            {
                var approvedRevision = r.Revisions.Where(r => r.Approvers.Count() > 0).LastOrDefault();
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
            TelemetryClient telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault());

            // Enabling this only for manual reviews in the beginning to check impact on system performance
            // We will enable it for all reviews based on the perf details
            // Automatic reviews are already updated as part of scheduled upload daily
            var reviews = await _reviewsRepository.GetReviewsAsync(false, "All");
            foreach(var review in reviews.Where(r => IsUpdateAvailable(r)))
            {
                var requestTelemetry = new RequestTelemetry { Name = "Updating Review " + review.ReviewId };
                var operation = telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    await Task.Delay(5000);
                    await UpdateReviewAsync(review);
                }
                catch (Exception e)
                {
                    telemetryClient.TrackException(e);
                }
                finally
                {
                    telemetryClient.StopOperation(operation);
                }
            }
        }
    }
}