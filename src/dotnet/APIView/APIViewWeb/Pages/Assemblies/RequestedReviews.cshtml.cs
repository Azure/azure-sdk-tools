using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace APIViewWeb.Pages.Assemblies
{    
    public class RequestedReviews: PageModel
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        public readonly UserProfileCache _userProfileCache;
        public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ActiveAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ApprovedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> NamespaceApprovalRequestedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ReviewsWithoutNamespaceApproval { get; set; } = new List<APIRevisionListItemModel>();
        
        // Track namespace approval request info for associated revisions
        public Dictionary<string, (DateTime RequestedOn, string RequestedBy)> NamespaceApprovalInfo { get; set; } = new Dictionary<string, (DateTime, string)>();
        
        public RequestedReviews(IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager, IPullRequestManager pullRequestManager, UserProfileCache userProfileCache, IConfiguration configuration, IMemoryCache cache)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _pullRequestManager = pullRequestManager;
            _userProfileCache = userProfileCache;
            _configuration = configuration;
            _cache = cache;
        }        
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.GetGitHubLogin();
            
            var isConfiguredApprover = IsUserConfiguredApprover(userId);
            
            UserProfileModel userProfile = null;
            var approvedLanguages = new string[0];
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    userProfile = await _userProfileCache.GetUserProfileAsync(userId);
                }
                approvedLanguages = userProfile?.Preferences?.ApprovedLanguages?.ToArray() ?? new string[0];
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer")) { }
            catch (Exception) { }
            
            IEnumerable<APIRevisionListItemModel> assignedRevisions;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    assignedRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);
                }
            }
            catch (OperationCanceledException)
            {
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            catch (Exception)
            {
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            
            APIRevisions = assignedRevisions;

            var activeAPIRevs = new List<APIRevisionListItemModel>();
            var approvedAPIRevs = new List<APIRevisionListItemModel>();
            var namespaceApprovalAPIRevs = new List<APIRevisionListItemModel>();

            var allNamespaceApprovalReviews = new List<APIRevisionListItemModel>();
            if (isConfiguredApprover)
            {
                try 
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        allNamespaceApprovalReviews = await _reviewManager.GetPendingNamespaceApprovalsBatchAsync(User, 100);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex.Message.Contains("AAD groups are being resolved")) { }
                catch (Exception) { }
            }
            
            var allReviews = APIRevisions.ToList();
            foreach (var nsReview in allNamespaceApprovalReviews)
            {
                if (!allReviews.Any(r => r.ReviewId == nsReview.ReviewId))
                {
                    allReviews.Add(nsReview);
                }
                
                if (nsReview.NamespaceApprovalRequestedOn.HasValue && !string.IsNullOrEmpty(nsReview.NamespaceApprovalRequestedBy))
                {
                    NamespaceApprovalInfo[nsReview.Id] = (
                        RequestedOn: nsReview.NamespaceApprovalRequestedOn.Value,
                        RequestedBy: nsReview.NamespaceApprovalRequestedBy
                    );
                }
            }
            APIRevisions = allReviews;

            var reviewIds = APIRevisions.Select(r => r.ReviewId).Distinct().ToList();
            var reviews = new Dictionary<string, ReviewListItemModel>();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    var reviewTasks = reviewIds.Select(async reviewId =>
                    {
                        try
                        {
                            var review = await _reviewManager.GetReviewAsync(User, reviewId);
                            return (reviewId, review);
                        }
                        catch (Exception)
                        {
                            return (reviewId, (ReviewListItemModel)null);
                        }
                    });

                    var reviewResults = await Task.WhenAll(reviewTasks);
                    foreach (var (rid, review) in reviewResults)
                    {
                        if (review != null)
                        {
                            reviews[rid] = review;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }

            foreach (var apiRevision in APIRevisions.OrderByDescending(r => r.AssignedReviewers.Any() ? r.AssignedReviewers.Where(x => x.AssingedTo.Equals(userId)).FirstOrDefault()?.AssingedOn ?? r.CreatedOn : r.CreatedOn))
            {
                bool isAssignedToUser = apiRevision.AssignedReviewers.Any(x => x.AssingedTo.Equals(userId));
                
                if (isAssignedToUser)
                {
                    if (!apiRevision.IsApproved)
                    {
                        activeAPIRevs.Add(apiRevision);
                    }

                    if (apiRevision.IsApproved && apiRevision.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn >= DateTime.Now.AddDays(-7))
                    {
                        approvedAPIRevs.Add(apiRevision);
                    }
                }

                if (reviews.TryGetValue(apiRevision.ReviewId, out var parentReview))
                {
                    bool isRevisionNamespaceRelated = parentReview.NamespaceReviewStatus == NamespaceReviewStatus.Pending || 
                                                    !string.IsNullOrEmpty(apiRevision.NamespaceApprovalRequestId) ||
                                                    apiRevision.IsNamespaceReviewRequested;
                    
                    if (isRevisionNamespaceRelated && !parentReview.IsApproved)
                    {
                        if (isAssignedToUser || isConfiguredApprover)
                        {
                            namespaceApprovalAPIRevs.Add(apiRevision);
                        }
                    }
                }
            }
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            
            ReviewsWithoutNamespaceApproval = new List<APIRevisionListItemModel>();
            
            ApprovedAPIRevisions.OrderByDescending(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn);

            return Page();
        }

        /// <summary>
        /// Check if the current user is configured as an approver in the application settings
        /// </summary>
        private bool IsUserConfiguredApprover(string userId)
        {
            var approverConfig = _configuration["approvers"];
            if (string.IsNullOrEmpty(approverConfig))
                return false;
            
            var configuredApprovers = approverConfig.Split(',').Select(a => a.Trim()).ToHashSet();
            return configuredApprovers.Contains(userId);
        }

        /// <summary>
        /// Check if the current user can approve a specific review based on configuration and user preferences
        /// </summary>
        private bool CanUserApproveReview(ReviewListItemModel review)
        {
            try
            {
                var preferredApprovers = PageModelHelpers.GetPreferredApprovers(_configuration, _userProfileCache, User, review);
                return preferredApprovers.Contains(User.GetGitHubLogin());
            }
            catch (Exception ex) when (ex.Message.Contains("AAD groups are being resolved"))
            {
                // For local testing, assume the user can approve TypeSpec reviews
                return review.Language?.Equals("TypeSpec", StringComparison.OrdinalIgnoreCase) == true;
            }
        }

        /// <summary>
        /// Get all reviews without namespace approval for proactive review
        /// Limited to SDK languages (C#, Java, Python, Go, JavaScript) and maximum 15 results
        /// Results are cached for 5 minutes to improve performance
        /// OPTIMIZED: Uses batch processing to minimize database calls
        /// </summary>
        private async Task<List<APIRevisionListItemModel>> GetReviewsWithoutNamespaceApproval()
        {
            // Disabled with UI; retain method for future use without being invoked
            await Task.CompletedTask;
            return new List<APIRevisionListItemModel>();
        }
    }
}
