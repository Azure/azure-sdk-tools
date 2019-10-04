// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
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

        private readonly IEnumerable<ILanguageService> _languageServices;

        public ReviewManager(
            IAuthorizationService authorizationService,
            CosmosReviewRepository reviewsRepository,
            BlobCodeFileRepository codeFileRepository,
            BlobOriginalsRepository originalsRepository,
            CosmosCommentsRepository commentsRepository,
            IEnumerable<ILanguageService> languageServices)
        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
            _commentsRepository = commentsRepository;
            _languageServices = languageServices;
        }

        public async Task<ReviewModel> CreateReviewAsync(ClaimsPrincipal user, string originalName, Stream fileStream, bool runAnalysis)
        {
            ReviewModel review = new ReviewModel();
            review.Author = user.GetGitHubLogin();
            review.CreationDate = DateTime.UtcNow;

            review.RunAnalysis = runAnalysis;

            var revision = new ReviewRevisionModel();
            var reviewCodeFileModel = await CreateFileAsync(revision.RevisionId, originalName, fileStream, runAnalysis);
            revision.Files.Add(reviewCodeFileModel);

            review.Name = reviewCodeFileModel.Name;
            review.Revisions.Add(revision);

            UpdateRevisionNames(review);
            await _reviewsRepository.UpsertReviewAsync(review);

            return review;
        }

        public Task<IEnumerable<ReviewModel>> GetReviewsAsync()
        {
            return _reviewsRepository.GetReviewsAsync();
        }

        public async Task DeleteReviewAsync(ClaimsPrincipal user, string id)
        {
            var reviewModel = await _reviewsRepository.GetReviewAsync(id);
            await AssertOwnerAsync(user, reviewModel);

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
            review.UpdateAvailable = review.Revisions
                .SelectMany(r=>r.Files)
                .Any(f => f.HasOriginal && GetLanguageService(f.Language).CanUpdate(f.VersionString));

            // Handle old model
#pragma warning disable CS0618 // Type or member is obsolete
            if (review.Revisions.Count == 0 && review.Files.Count == 1)
            {
                var file = review.Files[0];
#pragma warning restore CS0618 // Type or member is obsolete
                review.Revisions.Add(new ReviewRevisionModel()
                {
                    RevisionId = file.ReviewFileId,
                    Files =
                    {
                        file
                    }
                });
            }
            return review;
        }

        internal async Task UpdateReviewAsync(ClaimsPrincipal user, string id)
        {
            var review = await GetReviewAsync(user, id);
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

                    var codeFile = await languageService.GetCodeFileAsync(file.Name, fileOriginal, review.RunAnalysis);
                    await _codeFileRepository.UpsertCodeFileAsync(revision.RevisionId, file.ReviewFileId, codeFile);

                    InitializeFromCodeFile(file, codeFile);
                }
            }

            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task AddRevisionAsync(ClaimsPrincipal user, string id, string originalName, Stream fileStream)
        {
            var review = await GetReviewAsync(user, id);
            await AssertOwnerAsync(user, review);

            var revision = new ReviewRevisionModel();
            revision.Files.Add(await CreateFileAsync(revision.RevisionId, originalName, fileStream, review.RunAnalysis));
            review.Revisions.Add(revision);

            UpdateRevisionNames(review);
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        private async Task<ReviewCodeFileModel> CreateFileAsync(string revisionId, string originalName, Stream fileStream, bool runAnalysis)
        {
            var originalNameExtension = Path.GetExtension(originalName);
            var languageService = _languageServices.Single(s => s.IsSupportedExtension(originalNameExtension));

            var reviewCodeFileModel = new ReviewCodeFileModel();
            reviewCodeFileModel.HasOriginal = true;
            reviewCodeFileModel.Name = originalName;

            using (var memoryStream = new MemoryStream())
            {
                await fileStream.CopyToAsync(memoryStream);

                memoryStream.Position = 0;

                CodeFile codeFile = await languageService.GetCodeFileAsync(originalName, memoryStream, runAnalysis);

                InitializeFromCodeFile(reviewCodeFileModel, codeFile);

                memoryStream.Position = 0;
                await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.ReviewFileId, memoryStream);
                await _codeFileRepository.UpsertCodeFileAsync(revisionId, reviewCodeFileModel.ReviewFileId, codeFile);
            }

            return reviewCodeFileModel;
        }

        private void UpdateRevisionNames(ReviewModel review)
        {
            for (int i = 0; i < review.Revisions.Count; i++)
            {
                var reviewRevisionModel = review.Revisions[i];
                reviewRevisionModel.Name = $"rev {i} - {reviewRevisionModel.Files.Single().Name}";
            }
        }

        public async Task DeleteRevisionAsync(ClaimsPrincipal user, string id, string revisionId)
        {
            var review = await GetReviewAsync(user, id);
            await AssertOwnerAsync(user, review);
            if (review.Revisions.Count < 2)
            {
                return;
            }
            review.Revisions.RemoveAll(r => r.RevisionId == revisionId);
            UpdateRevisionNames(review);
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        private void InitializeFromCodeFile(ReviewCodeFileModel file, CodeFile codeFile)
        {
            file.Language = codeFile.Language;
            file.VersionString = codeFile.VersionString;
        }

        private ILanguageService GetLanguageService(string language)
        {
            return _languageServices.Single(service => service.Name == language);
        }

        private async Task AssertOwnerAsync(ClaimsPrincipal user, ReviewModel reviewModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, reviewModel, new[] { ReviewOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}