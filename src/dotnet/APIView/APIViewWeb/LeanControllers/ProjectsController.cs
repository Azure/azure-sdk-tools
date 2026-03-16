using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers
{
    public class ProjectsController : BaseApiController
    {
        private readonly ILogger<ProjectsController> _logger;
        private readonly ICosmosProjectRepository _projectsRepository;
        private readonly ICosmosReviewRepository _reviewsRepository;
        private readonly INamespaceManager _namespaceManager;

        public ProjectsController(
            ILogger<ProjectsController> logger,
            ICosmosProjectRepository projectsRepository,
            ICosmosReviewRepository reviewsRepository,
            INamespaceManager namespaceManager)
        {
            _logger = logger;
            _projectsRepository = projectsRepository;
            _reviewsRepository = reviewsRepository;
            _namespaceManager = namespaceManager;
        }

        /// <summary>
        /// Get a project by its ID
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <returns>The project</returns>
        [HttpGet("{projectId}", Name = "GetProject")]
        public async Task<ActionResult<Project>> GetProjectAsync(string projectId)
        {
            var project = await _projectsRepository.GetProjectAsync(projectId);
            if (project == null)
            {
                _logger.LogWarning("Project with ID {ProjectId} was not found.", projectId);
                return NotFound();
            }
            return new LeanJsonResult(project, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Get all reviews that belong to a project
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <returns>List of reviews in the project</returns>
        [HttpGet("{projectId}/reviews", Name = "GetProjectReviews")]
        public async Task<ActionResult<IEnumerable<ReviewListItemModel>>> GetProjectReviewsAsync(string projectId)
        {
            var project = await _projectsRepository.GetProjectAsync(projectId);
            if (project == null)
            {
                return NotFound();
            }

            var relatedReviews = new List<ReviewListItemModel>();

            if (project.Reviews is { Count: > 0 })
            {
                var reviews = await _reviewsRepository.GetReviewsAsync(project.Reviews.Values);
                relatedReviews = reviews
                    .Where(r => r is { IsDeleted: false })
                    .OrderBy(r => r.Language)
                    .ToList();
            }

            return new LeanJsonResult(relatedReviews, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Get related reviews for a specific review (reviews in the same project)
        /// </summary>
        /// <param name="reviewId">The review ID to find related reviews for</param>
        /// <returns>List of related reviews in the same project</returns>
        [HttpGet("reviews/{reviewId}/related", Name = "GetRelatedReviews")]
        public async Task<ActionResult<RelatedReviewsResponse>> GetRelatedReviewsAsync(string reviewId)
        {
            var review = await _reviewsRepository.GetReviewAsync(reviewId);
            if (review == null)
            {
                return NotFound();
            }

            var response = new RelatedReviewsResponse
            {
                CurrentReviewId = reviewId,
                ProjectId = review.ProjectId,
                Reviews = []
            };

            if (string.IsNullOrEmpty(review.ProjectId))
            {
                return new LeanJsonResult(response, StatusCodes.Status200OK);
            }

            var project = await _projectsRepository.GetProjectAsync(review.ProjectId);
            if (project == null)
            {
                return new LeanJsonResult(response, StatusCodes.Status200OK);
            }

            response.ProjectName = project.DisplayName;
            response.CrossLanguagePackageId = project.CrossLanguagePackageId;

            if (project.Reviews is { Count: > 0 })
            {
                foreach (string relatedReviewId in project.Reviews.Values)
                {
                    ReviewListItemModel relatedReview = await _reviewsRepository.GetReviewAsync(relatedReviewId);
                    if (relatedReview is { IsDeleted: false })
                    {
                        response.Reviews.Add(relatedReview);
                    }
                }
            }

            // Sort: TypeSpec first, then by language name
            response.Reviews = response.Reviews
                .OrderBy(r => r.Language == "TypeSpec" ? 0 : 1)
                .ThenBy(r => r.Language)
                .ToList();

            return new LeanJsonResult(response, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Get namespace info for a project (approved namespaces + history)
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <returns>ProjectNamespaceInfo</returns>
        [HttpGet("{projectId}/namespaces", Name = "GetProjectNamespaces")]
        public async Task<ActionResult<ProjectNamespaceInfo>> GetProjectNamespacesAsync(string projectId)
        {
            var namespaceInfo = await _namespaceManager.GetNamespaceInfoAsync(projectId);
            if (namespaceInfo == null)
            {
                return NotFound();
            }
            return new LeanJsonResult(namespaceInfo, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Approve a proposed namespace for a language
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <param name="language">The language to approve the namespace for</param>
        /// <returns>The updated project</returns>
        [HttpPost("{projectId}/namespaces/{language}/approve", Name = "ApproveNamespace")]
        public async Task<ActionResult<Project>> ApproveNamespaceAsync(string projectId, string language)
        {
            var result = await _namespaceManager.ApproveNamespaceAsync(projectId, language, User);
            if (!result.IsSuccess)
            {
                return MapNamespaceError(result.Error!.Value, language, "approve");
            }
            return new LeanJsonResult(result.Project, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Reject a proposed namespace for a language
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <param name="language">The language to reject the namespace for</param>
        /// <param name="request">Optional rejection notes</param>
        /// <returns>The updated project</returns>
        [HttpPost("{projectId}/namespaces/{language}/reject", Name = "RejectNamespace")]
        public async Task<ActionResult<Project>> RejectNamespaceAsync(string projectId, string language, [FromBody] NamespaceDecisionRequest request = null)
        {
            var result = await _namespaceManager.RejectNamespaceAsync(projectId, language, request?.Notes, User);
            if (!result.IsSuccess)
            {
                return MapNamespaceError(result.Error!.Value, language, "reject");
            }
            return new LeanJsonResult(result.Project, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Withdraw a proposed namespace for a language
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <param name="language">The language to withdraw the namespace for</param>
        /// <returns>The updated project</returns>
        [HttpPost("{projectId}/namespaces/{language}/withdraw", Name = "WithdrawNamespace")]
        public async Task<ActionResult<Project>> WithdrawNamespaceAsync(string projectId, string language)
        {
            var result = await _namespaceManager.WithdrawNamespaceAsync(projectId, language, User);
            if (!result.IsSuccess)
            {
                return MapNamespaceError(result.Error!.Value, language, "withdraw");
            }
            return new LeanJsonResult(result.Project, StatusCodes.Status200OK);
        }

        private ActionResult MapNamespaceError(NamespaceOperationError error, string language, string action)
        {
            return error switch
            {
                NamespaceOperationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden,
                    new { message = $"Insufficient permissions to {action} the namespace for language '{language}'. Please contact an approver for this language." }),
                NamespaceOperationError.ProjectNotFound => NotFound(
                    new { message = "The specified project was not found or does not have namespace information configured." }),
                NamespaceOperationError.LanguageNotFound => NotFound(
                    new { message = $"No namespace proposal exists for language '{language}' in this project." }),
                NamespaceOperationError.InvalidStateTransition => Conflict(
                    new { message = $"The namespace for '{language}' cannot be {GetPastTense(action)} in its current state." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An unexpected error occurred while processing the namespace operation. Please try again or contact support." })
            };
        }
        private static string GetPastTense(string action) => action switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            "withdraw" => "withdrawn",
            _ => action
        };
    }

    public class NamespaceDecisionRequest
    {
        public string Notes { get; set; }
    }
    
    public class RelatedReviewsResponse
    {
        public string CurrentReviewId { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string CrossLanguagePackageId { get; set; }
        public List<ReviewListItemModel> Reviews { get; set; }
    }
}
