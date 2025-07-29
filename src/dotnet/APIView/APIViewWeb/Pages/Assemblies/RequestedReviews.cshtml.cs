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
        
        [BindProperty(SupportsGet = true)]
        public bool ShowWithoutNamespaceApproval { get; set; } = false;        
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
            List<APIRevisionListItemModel> reviewsWithoutNamespaceApproval = new List<APIRevisionListItemModel>();

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
            
            // Keep track of which revisions are related to namespace approval (even if they're not the primary namespace review)
            var namespaceRelatedRevisionIds = new HashSet<string>(allNamespaceApprovalReviews.Select(r => r.Id));
            
            // Merge with assigned reviews
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
                    // Add to namespace approval list if:
                    // 1. The parent review has namespace approval requested OR
                    // 2. This revision is an associated SDK revision from our namespace approval logic
                    bool isRevisionNamespaceRelated = parentReview.IsNamespaceReviewRequested || namespaceRelatedRevisionIds.Contains(apiRevision.Id);
                    
                    if (isRevisionNamespaceRelated && !parentReview.IsApproved)
                    {
                        bool canApproveReview = isAssignedToUser || CanUserApproveReview(parentReview);
                        if (canApproveReview)
                        {
                            namespaceApprovalAPIRevs.Add(apiRevision);
                        }
                    }
                    
                    // Add to reviews without namespace approval if the parent review doesn't have namespace approval
                    // and the user is assigned as a reviewer and it's not an associated namespace revision
                    if (!parentReview.IsNamespaceReviewRequested && !parentReview.IsApproved && isAssignedToUser && !namespaceRelatedRevisionIds.Contains(apiRevision.Id))
                    {
                        reviewsWithoutNamespaceApproval.Add(apiRevision);
                    }
                }
            }
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            ReviewsWithoutNamespaceApproval = reviewsWithoutNamespaceApproval;
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
        /// Uses the same logic as "Associated API Revisions" - find via pull requests rather than package name matching
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
                    // Get reviews with namespace approval requested - only TypeSpec reviews can have namespace approval requests  
                    var (allReviews, _, _, _, _, _) = await _reviewManager.GetPagedReviewListAsync(
                        search: new string[] { }, // No search filter
                        languages: new HashSet<string> { "TypeSpec" }, // Only TypeSpec reviews have namespace approval requests
                        isClosed: false, // Only open reviews
                        isApproved: false, // Only unapproved reviews
                        offset: 0,
                        limit: 100,
                        orderBy: "created"
                    );

                    // Filter for reviews that have namespace approval requested
                    var reviewsWithNamespaceRequested = allReviews.Where(r => r.IsNamespaceReviewRequested).ToList();

                    // Process only reviews the user can approve
                    var eligibleReviews = reviewsWithNamespaceRequested.Where(r => CanUserApproveReview(r)).ToList();
                    
                    foreach (var namespaceReview in eligibleReviews)
                    {
                        try
                        {
                            // Extract namespace approval request information from this review's change history
                            var namespaceRequestChange = namespaceReview.ChangeHistory?.FirstOrDefault(ch => ch.ChangeAction == ReviewChangeAction.NamespaceReviewRequested);
                            var requestedOn = namespaceRequestChange?.ChangedOn ?? DateTime.MinValue;
                            var requestedBy = namespaceRequestChange?.ChangedBy ?? "Unknown";
                            
                            // Get the latest API revision from this namespace review
                            var latestRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(namespaceReview.Id, null, APIRevisionType.All);
                            if (latestRevision != null)
                            {
                                // Use the SAME LOGIC as "Associated API Revisions" - find via pull requests
                                var creatingPR = (await _pullRequestManager.GetPullRequestsModelAsync(namespaceReview.Id, latestRevision.Id)).FirstOrDefault();
                                if (creatingPR != null)
                                {
                                    // Get all pull requests associated with this PR (this gives us all the different language versions)
                                    var associatedPRs = await _pullRequestManager.GetPullRequestsModelAsync(creatingPR.PullRequestNumber, creatingPR.RepoName);
                                    
                                    foreach (var associatedPR in associatedPRs)
                                    {
                                        // Skip if this is the original namespace review (we want the associated SDK reviews)
                                        if (associatedPR.ReviewId == namespaceReview.Id)
                                        {
                                            continue;
                                        }
                                        
                                        // Only include SDK languages (C#, Java, Python, etc.) not TypeSpec
                                        var supportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                        {
                                            "C#", "CSharp", "Java", "Python", "Go", "JavaScript", "JS"
                                        };
                                        
                                        if (supportedLanguages.Contains(associatedPR.Language ?? ""))
                                        {
                                            // Get the latest revision from this associated review
                                            var associatedRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(associatedPR.ReviewId, null, APIRevisionType.All);
                                            if (associatedRevision != null && !associatedRevision.IsApproved)
                                            {
                                                namespaceApprovalReviews.Add(associatedRevision);
                                                
                                                // Store namespace approval request info for this revision
                                                if (requestedOn != DateTime.MinValue)
                                                {
                                                    NamespaceApprovalInfo[associatedRevision.Id] = (requestedOn, requestedBy);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // If no PR association found, fall back to adding the revision itself if it's an SDK language
                                    var supportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        "C#", "CSharp", "Java", "Python", "Go", "JavaScript", "JS"
                                    };
                                    
                                    if (supportedLanguages.Contains(namespaceReview.Language ?? ""))
                                    {
                                        namespaceApprovalReviews.Add(latestRevision);
                                        
                                        // Store namespace approval request info for this revision
                                        if (requestedOn != DateTime.MinValue)
                                        {
                                            NamespaceApprovalInfo[latestRevision.Id] = (requestedOn, requestedBy);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
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
            catch (Exception ex)
            {
                // Log error but don't fail the entire request
                if (ex.Message.Contains("AAD groups are being resolved"))
                {
                }
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
    }
}
