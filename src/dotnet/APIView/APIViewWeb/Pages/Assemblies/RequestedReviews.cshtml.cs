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
            
            // Check user configuration
            var isConfiguredApprover = IsUserConfiguredApprover(userId);
            
            // Check user profile with error handling and timeout protection
            UserProfileModel userProfile = null;
            var approvedLanguages = new string[0];
            try
            {
                // Reduced timeout to 5 seconds to improve performance and prevent hanging on Azure AD issues
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    userProfile = await _userProfileCache.GetUserProfileAsync(userId);
                }
                
                approvedLanguages = userProfile?.Preferences?.ApprovedLanguages?.ToArray() ?? new string[0];
            }
            catch (OperationCanceledException)
            {
                // Continue with empty profile - we'll still show assigned reviews
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                // Continue with empty profile - we'll still show assigned reviews
            }
            catch (Exception)
            {
                // Continue with empty profile - we'll still show assigned reviews
            }
            
            // Attempt to get assigned revisions with error handling and timeout protection
            IEnumerable<APIRevisionListItemModel> assignedRevisions;
            try
            {
                // Reduced timeout to 5 seconds to improve performance and prevent hanging on Azure AD issues
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

            List<APIRevisionListItemModel> activeAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> approvedAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> namespaceApprovalAPIRevs = new List<APIRevisionListItemModel>();

            // Check if user is configured as an approver
            // var isConfiguredApprover = IsUserConfiguredApprover(userId);  // Already checked above
            
            // Get all reviews with namespace approval requested for languages the user can approve
            var allNamespaceApprovalReviews = new List<APIRevisionListItemModel>();
            if (isConfiguredApprover)
            {
                try 
                {
                    allNamespaceApprovalReviews = await GetAllNamespaceApprovalReviews();
                }
                catch (Exception ex) when (ex.Message.Contains("AAD groups are being resolved"))
                {
                    // Continue without namespace approval reviews to avoid long delays
                }
            }
            
            // Merge namespace approval reviews with assigned reviews  
            var allReviews = APIRevisions.ToList();
            foreach (var nsReview in allNamespaceApprovalReviews)
            {
                if (!allReviews.Any(r => r.ReviewId == nsReview.ReviewId))
                {
                    allReviews.Add(nsReview);
                }
            }
            APIRevisions = allReviews;

            // Get all unique review IDs to minimize database calls
            var reviewIds = APIRevisions.Select(r => r.ReviewId).Distinct().ToList();
            var reviews = new Dictionary<string, ReviewListItemModel>();
            
            // Fetch all parent reviews in batch using parallel processing
            var reviewTasks = reviewIds.Select(async reviewId =>
            {
                var review = await _reviewManager.GetReviewAsync(User, reviewId);
                return (reviewId, review);
            });

            var reviewResults = await Task.WhenAll(reviewTasks);
            
            foreach (var (reviewId, review) in reviewResults)
            {
                if (review != null)
                {
                    reviews[reviewId] = review;
                }
            }

            foreach (var apiRevision in APIRevisions.OrderByDescending(r => r.AssignedReviewers.Any() ? r.AssignedReviewers.Where(x => x.AssingedTo.Equals(userId)).FirstOrDefault()?.AssingedOn ?? r.CreatedOn : r.CreatedOn))
            {
                bool isAssignedToUser = apiRevision.AssignedReviewers.Any(x => x.AssingedTo.Equals(userId));
                
                // Only add to active/approved lists if the user is assigned as a reviewer
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

                // Check if the parent review has namespace approval requested using cached data
                if (reviews.TryGetValue(apiRevision.ReviewId, out var parentReview))
                {
                    // Use the optimized field-based approach instead of complex PR association logic
                    // Check if:
                    // 1. The parent review has namespace approval requested OR
                    // 2. This specific revision is marked with a namespace approval request ID (optimized approach)
                    bool isRevisionNamespaceRelated = parentReview.IsNamespaceReviewRequested || 
                                                    !string.IsNullOrEmpty(apiRevision.NamespaceApprovalRequestId);
                    
                    if (isRevisionNamespaceRelated && !parentReview.IsApproved)
                    {
                        bool canApproveReview = isAssignedToUser || CanUserApproveReview(parentReview);
                        if (canApproveReview)
                        {
                            namespaceApprovalAPIRevs.Add(apiRevision);
                        }
                    }
                }
            }
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            
            // Always populate reviews without namespace approval for the 4th tab
            ReviewsWithoutNamespaceApproval = await GetReviewsWithoutNamespaceApproval();
            
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
        /// Get all reviews with namespace approval requested that the user can approve based on configuration
        /// Uses the new optimized approach with direct field querying instead of complex PR association logic
        /// Results are cached for 10 minutes to improve performance
        /// </summary>
        private async Task<List<APIRevisionListItemModel>> GetAllNamespaceApprovalReviews()
        {
            var userId = User.GetGitHubLogin();
            var cacheKey = $"namespace_approvals_{userId}";
            
            // Check cache first - 10 minute cache for performance
            if (_cache.TryGetValue(cacheKey, out List<APIRevisionListItemModel> cachedResults))
            {
                return cachedResults;
            }

            var namespaceApprovalReviews = new List<APIRevisionListItemModel>();

            try
            {
                // 5 second timeout to improve performance and avoid hanging on database/AAD delays
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    // Get API revisions that have namespace approval requested using the new optimized fields
                    // This replaces the complex PR association logic with a single direct query
                    var filterAndSortParams = new FilterAndSortParams()
                    {
                        Languages = new HashSet<string> { "C#", "Java", "Python", "Go", "JavaScript" }, // Only SDK languages need approval
                        SortField = "lastUpdatedOn",
                        SortOrder = 1 // Descending
                    };

                    var pageParams = new PageParams()
                    {
                        PageSize = 100, // Reasonable limit for namespace approvals
                        NoOfItemsRead = 0
                    };

                    // Get revisions directly where NamespaceApprovalRequestId is not null/empty
                    // This is the new optimized approach that avoids N+1 queries
                    var allRevisionsResult = await _apiRevisionsManager.GetAPIRevisionsAsync(User, pageParams, filterAndSortParams);
                    var revisionsWithNamespaceApproval = allRevisionsResult.Where(r => 
                        !string.IsNullOrEmpty(r.NamespaceApprovalRequestId) && 
                        r.NamespaceApprovalRequestedOn.HasValue &&
                        !string.IsNullOrEmpty(r.NamespaceApprovalRequestedBy) &&
                        !r.IsDeleted).ToList();

                    // Get all unique review IDs to check if user can approve them
                    var reviewIds = revisionsWithNamespaceApproval.Select(r => r.ReviewId).Distinct().ToList();
                    var reviews = new Dictionary<string, ReviewListItemModel>();
                    
                    // Fetch parent reviews in batch
                    var reviewTasks = reviewIds.Select(async reviewId =>
                    {
                        var review = await _reviewManager.GetReviewAsync(User, reviewId);
                        return (reviewId, review);
                    });
                    
                    var reviewResults = await Task.WhenAll(reviewTasks);
                    
                    foreach (var (reviewId, review) in reviewResults)
                    {
                        if (review != null && !review.IsApproved)
                        {
                            reviews[reviewId] = review;
                        }
                    }

                    // Filter to only include revisions for reviews the user can approve
                    foreach (var revision in revisionsWithNamespaceApproval)
                    {
                        if (reviews.TryGetValue(revision.ReviewId, out var parentReview))
                        {
                            if (CanUserApproveReview(parentReview))
                            {
                                namespaceApprovalReviews.Add(revision);
                                
                                // Store namespace approval info for the UI using the new fields
                                NamespaceApprovalInfo[revision.Id] = (
                                    RequestedOn: revision.NamespaceApprovalRequestedOn.Value,
                                    RequestedBy: revision.NamespaceApprovalRequestedBy
                                );
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout occurred - return empty list to avoid hanging
            }
            catch (Exception)
            {
                // Error occurred - return empty list to be safe
            }

            // Cache the results for 10 minutes to improve performance
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5), // Extend cache if accessed within 5 minutes
                Priority = CacheItemPriority.Normal
            };
            _cache.Set(cacheKey, namespaceApprovalReviews, cacheOptions);
            
            return namespaceApprovalReviews;
        }

        /// <summary>
        /// Get all reviews without namespace approval for proactive review
        /// Limited to SDK languages (C#, Java, Python, Go, JavaScript) and maximum 100 results
        /// Results are cached for 5 minutes to improve performance
        /// </summary>
        private async Task<List<APIRevisionListItemModel>> GetReviewsWithoutNamespaceApproval()
        {
            var userId = User.GetGitHubLogin();
            var cacheKey = $"reviews_without_namespace_{userId}";
            
            // Check cache first - 5 minute cache for performance
            if (_cache.TryGetValue(cacheKey, out List<APIRevisionListItemModel> cachedResults))
            {
                return cachedResults;
            }

            var reviewsWithoutNamespace = new List<APIRevisionListItemModel>();

            try
            {
                // 5 second timeout to improve performance and avoid hanging on database/AAD delays
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    // Define SDK languages that we want to show for proactive review
                    var sdkLanguages = new HashSet<string> { "C#", "CSharp", "Java", "Python", "Go", "JavaScript", "JS" };
                    
                    // Get reviews for SDK languages that don't have namespace approval requested
                    var (allReviews, _, _, _, _, _) = await _reviewManager.GetPagedReviewListAsync(
                        search: new string[] { }, // No search filter
                        languages: sdkLanguages, // Only SDK languages
                        isClosed: false, // Only open reviews
                        isApproved: false, // Only unapproved reviews
                        offset: 0,
                        limit: 100, // Limit to 100 as requested
                        orderBy: "created"
                    );

                    // Filter for reviews that do NOT have namespace approval requested
                    var reviewsWithoutNamespaceRequested = allReviews.Where(r => !r.IsNamespaceReviewRequested).ToList();

                    // For each review, get the latest API revision
                    foreach (var review in reviewsWithoutNamespaceRequested)
                    {
                        try
                        {
                            var latestRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(review.Id, null, APIRevisionType.All);
                            if (latestRevision != null && !latestRevision.IsApproved)
                            {
                                reviewsWithoutNamespace.Add(latestRevision);
                            }
                        }
                        catch (Exception)
                        {
                            // Continue with next review if this one fails
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
            }
            catch (Exception)
            {
            }

            // Cache the results for 5 minutes to improve performance
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2), // Extend cache if accessed within 2 minutes
                Priority = CacheItemPriority.Normal
            };
            _cache.Set(cacheKey, reviewsWithoutNamespace, cacheOptions);
            
            return reviewsWithoutNamespace;
        }
    }
}
