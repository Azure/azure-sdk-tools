// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Hubs;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http;
using System.Text.Json;
using System.Data;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using APIViewWeb.Managers.Interfaces;

namespace APIViewWeb.Managers
{
    public class ReviewManager : IReviewManager
    {

        private readonly IAuthorizationService _authorizationService;
        private readonly ICosmosReviewRepository _reviewsRepository;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ICommentsManager _commentManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly IHubContext<SignalRHub> _signalRHubContext;

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, ICosmosCommentsRepository commentsRepository, 
            IHubContext<SignalRHub> signalRHubContext)

        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _apiRevisionsManager = apiRevisionsManager;
            _commentManager = commentManager;
            _codeFileRepository = codeFileRepository;
            _commentsRepository = commentsRepository;
            _signalRHubContext = signalRHubContext;
        }

        public Task<ReviewListItemModel> GetReviewAsync(string language, string packageName, bool isClosed = false)
        {
            return _reviewsRepository.GetReviewAsync(language, packageName, isClosed);
        }
        /// <summary>
        /// Get Reviews that have been assigned for review to a user
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public async Task<IEnumerable<ReviewListItemModel>> GetReviewsAssignedToUser(string userName)
        {
            return await _reviewsRepository.GetReviewsAssignedToUser(userName);
        }

        /// <summary>
        /// Get List of Reviews for the Review Page
        /// </summary>
        /// <param name="search"></param>
        /// <param name="languages"></param>
        /// <param name="isClosed"></param>
        /// <param name="isApproved"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        public async Task<(IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage)> GetPagedReviewListAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, bool? isApproved, int offset, int limit, string orderBy)
        {
            var result = await _reviewsRepository.GetReviewsAsync(search: search, languages: languages, isClosed: isClosed, isApproved:  isApproved, offset: offset, limit: limit, orderBy: orderBy);

            // Calculate and add Previous and Next and Current page to the returned result
            var totalPages = (int)Math.Ceiling(result.TotalCount / (double)limit);
            var currentPage = offset == 0 ? 1 : offset / limit + 1;

            (IEnumerable<ReviewListItemModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage) resultToReturn = (
                result.Reviews, result.TotalCount, TotalPages: totalPages,
                CurrentPage: currentPage,
                PreviousPage: currentPage == 1 ? null : currentPage - 1,
                NextPage: currentPage >= totalPages ? null : currentPage + 1
            );
            return resultToReturn;
        }

        /// <summary>
        /// SoftDeleteReviewAsync
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task SoftDeleteReviewAsync(ClaimsPrincipal user, string id)
        {
            var review = await _reviewsRepository.GetReviewAsync(id);
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(id);
            await AssertReviewOwnerAsync(user, review);

            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(review.ChangeHistory, ReviewChangeAction.Deleted, user.GetGitHubLogin());
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsDeleted = changeUpdate.ChangeStatus;
            await _reviewsRepository.UpsertReviewAsync(review);

            foreach (var revision in revisions)
            {
                await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(user, revision);
            }
            await _commentManager.SoftDeleteCommentsAsync(user, review.Id);
        }

        /// <summary>
        /// Get Reviews
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public async Task<ReviewListItemModel> GetReviewAsync(ClaimsPrincipal user, string id)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var review = await _reviewsRepository.GetReviewAsync(id);
            return review;
        }

        /// <summary>
        /// Toggle Review Open/Closed state
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task ToggleReviewIsClosedAsync(ClaimsPrincipal user, string id)
        {
            var review = await _reviewsRepository.GetReviewAsync(id);
            var userId = user.GetGitHubLogin();
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Closed, userId);
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsClosed = changeUpdate.ChangeStatus;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        /// <summary>
        /// Add new Approval or ApprovalReverted action to the ChangeHistory of a Review. Serves as firstRelease approval
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <param name="revisionId"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public async Task ToggleReviewApprovalAsync(ClaimsPrincipal user, string id, string revisionId, string notes="")
        {
            ReviewListItemModel review = await _reviewsRepository.GetReviewAsync(id);
            await ManagerHelpers.AssertApprover<ReviewListItemModel>(user, review, _authorizationService);
            var userId = user.GetGitHubLogin();
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, userId, notes);
            review.ChangeHistory = changeUpdate.ChangeHistory;
            review.IsApproved = changeUpdate.ChangeStatus;

            await _reviewsRepository.UpsertReviewAsync(review);
            await _signalRHubContext.Clients.Group(userId).SendAsync("ReceiveApprovalSelf", id, revisionId, review.IsApproved);
            await _signalRHubContext.Clients.All.SendAsync("ReceiveApproval", id, revisionId, userId, review.IsApproved);
        }

        /// <summary>
        /// Assign reviewers to a review
        /// </summary>
        /// <param name="User"></param>
        /// <param name="reviewId"></param>
        /// <param name="reviewers"></param>
        /// <returns></returns>
        public async Task AssignReviewersToReviewAsync(ClaimsPrincipal User, string reviewId, HashSet<string> reviewers)
        {
            ReviewListItemModel review = await _reviewsRepository.GetReviewAsync(reviewId);
            foreach (var reviewer in reviewers)
            {
                if (!review.AssignedReviewers.Where(x => x.AssingedTo == reviewer).Any())
                {
                    review.AssignedReviewers.Append(new ReviewAssignmentModel()
                    {
                        AssingedTo = reviewer,
                        AssignedBy = User.GetGitHubLogin(),
                        AssingedOn = DateTime.Now,
                    });
                }
            }
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        /// <summary>
        /// Sends info to AI service for generating initial review on APIReview file
        /// </summary>
        public async Task<int> GenerateAIReview(string reviewId, string revisionId)
        {
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
            var revision = revisions.Where(r => r.Id == revisionId).FirstOrDefault();
            var codeFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var codeLines = codeFile.RenderText(false);

            var reviewText = new StringBuilder();
            foreach (var codeLine in codeLines)
            {
                reviewText.Append(codeLine.DisplayString);
                reviewText.Append("\\n");
            }

            var url = "https://apiview-gpt.azurewebsites.net/python";
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(20);
            var payload = new
            {
                content = reviewText.ToString()
            };

            var result = new AIReviewModel();
            try {
                var response = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var responseSanitized = JsonSerializer.Deserialize<string>(responseString);
                result = JsonSerializer.Deserialize<AIReviewModel>(responseSanitized);
            }
            catch (Exception e ) {
                throw new Exception($"Copilot Failed: {e.Message}");
            }
           
            // Write back result as comments to APIView
            foreach (var violation in result.Violations)
            {
                var codeLine = codeLines[violation.LineNo];
                var comment = new CommentItemModel();
                comment.CreatedOn = DateTime.UtcNow;
                comment.ReviewId = reviewId;
                comment.RevisionId = revisionId;
                comment.ElementId = codeLine.ElementId;
                //comment.SectionClass = sectionClass; // This will be needed for swagger

                var commentText = new StringBuilder();
                commentText.AppendLine($"Suggestion: `{violation.Suggestion}`");
                commentText.AppendLine();
                commentText.AppendLine(violation.Comment);
                foreach (var id in violation.RuleIds)
                {
                    commentText.AppendLine($"See: https://guidelinescollab.github.io/azure-sdk/{id}");
                }
                comment.ResolutionLocked = false;
                comment.CreatedBy = "azure-sdk";
                comment.CommentText = commentText.ToString();

                await _commentsRepository.UpsertCommentAsync(comment);
            }
            return result.Violations.Count;
        }

        private async Task AssertReviewOwnerAsync(ClaimsPrincipal user, ReviewListItemModel reviewModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, reviewModel, new[] { ReviewOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}
