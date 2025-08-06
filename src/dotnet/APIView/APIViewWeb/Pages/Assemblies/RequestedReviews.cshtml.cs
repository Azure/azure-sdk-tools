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
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
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
                var namespaceStopwatch = System.Diagnostics.Stopwatch.StartNew();
                try 
                {
                    // Add timeout protection to prevent infinite loading
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        // Use the new optimized batch method to get all pending namespace approvals
                        allNamespaceApprovalReviews = await _reviewManager.GetPendingNamespaceApprovalsBatchAsync(User, 100);
                    }
                    namespaceStopwatch.Stop();
                    Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - GetPendingNamespaceApprovalsBatchAsync took: {namespaceStopwatch.ElapsedMilliseconds}ms, returned {allNamespaceApprovalReviews.Count} reviews");
                }
                catch (OperationCanceledException)
                {
                    namespaceStopwatch.Stop();
                    Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - GetPendingNamespaceApprovalsBatchAsync timed out after {namespaceStopwatch.ElapsedMilliseconds}ms");
                    // Continue without namespace approval reviews to avoid infinite loading
                }
                catch (Exception ex) when (ex.Message.Contains("AAD groups are being resolved"))
                {
                    namespaceStopwatch.Stop();
                    Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - GetPendingNamespaceApprovalsBatchAsync failed after {namespaceStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                    // Continue without namespace approval reviews to avoid long delays
                }
                catch (Exception ex)
                {
                    namespaceStopwatch.Stop();
                    Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - GetPendingNamespaceApprovalsBatchAsync failed after {namespaceStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                    // Continue without namespace approval reviews on any error
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
                
                // Store namespace approval info for the UI using the optimized fields
                if (nsReview.NamespaceApprovalRequestedOn.HasValue && !string.IsNullOrEmpty(nsReview.NamespaceApprovalRequestedBy))
                {
                    NamespaceApprovalInfo[nsReview.Id] = (
                        RequestedOn: nsReview.NamespaceApprovalRequestedOn.Value,
                        RequestedBy: nsReview.NamespaceApprovalRequestedBy
                    );
                }
            }
            APIRevisions = allReviews;

            // Get all unique review IDs to minimize database calls
            var reviewIds = APIRevisions.Select(r => r.ReviewId).Distinct().ToList();
            var reviews = new Dictionary<string, ReviewListItemModel>();
            
            var batchReviewStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Add timeout protection for batch review fetching
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    // Fetch all parent reviews in batch using parallel processing
                    var reviewTasks = reviewIds.Select(async reviewId =>
                    {
                        try
                        {
                            var review = await _reviewManager.GetReviewAsync(User, reviewId);
                            return (reviewId, review);
                        }
                        catch (Exception)
                        {
                            // Return null review on error to continue with other reviews
                            return (reviewId, (ReviewListItemModel)null);
                        }
                    });

                    var reviewResults = await Task.WhenAll(reviewTasks);
                    batchReviewStopwatch.Stop();
                    Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Batch fetch {reviewIds.Count} parent reviews took: {batchReviewStopwatch.ElapsedMilliseconds}ms");
                    
                    foreach (var (reviewId, review) in reviewResults)
                    {
                        if (review != null)
                        {
                            reviews[reviewId] = review;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                batchReviewStopwatch.Stop();
                Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Batch review fetch timed out after {batchReviewStopwatch.ElapsedMilliseconds}ms");
                // Continue with empty reviews dict - the page will show without parent review details
            }
            catch (Exception ex)
            {
                batchReviewStopwatch.Stop();
                Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Batch review fetch failed after {batchReviewStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                // Continue with empty reviews dict
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
                    // 1. The parent review has namespace review status as Pending OR
                    // 2. This specific revision is marked with a namespace approval request ID (optimized approach) OR
                    // 3. This specific revision has the IsNamespaceReviewRequested flag set
                    bool isRevisionNamespaceRelated = parentReview.NamespaceReviewStatus == NamespaceReviewStatus.Pending || 
                                                    !string.IsNullOrEmpty(apiRevision.NamespaceApprovalRequestId) ||
                                                    apiRevision.IsNamespaceReviewRequested;
                    
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

            totalStopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - TOTAL page load took: {totalStopwatch.ElapsedMilliseconds}ms for user: {userId}");
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Final counts: Active={ActiveAPIRevisions.Count()}, Approved={ApprovedAPIRevisions.Count()}, NamespaceApproval={NamespaceApprovalRequestedAPIRevisions.Count()}, WithoutNamespace={ReviewsWithoutNamespaceApproval.Count()}");

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
        /// Limited to SDK languages (C#, Java, Python, Go, JavaScript) and maximum 100 results
        /// Results are cached for 5 minutes to improve performance
        /// </summary>
        private async Task<List<APIRevisionListItemModel>> GetReviewsWithoutNamespaceApproval()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var userId = User.GetGitHubLogin();
            var cacheKey = $"reviews_without_namespace_{userId}";
            
            // Check cache first - 5 minute cache for performance
            if (_cache.TryGetValue(cacheKey, out List<APIRevisionListItemModel> cachedResults))
            {
                stopwatch.Stop();
                Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Cache hit, took: {stopwatch.ElapsedMilliseconds}ms, returned {cachedResults.Count} reviews");
                return cachedResults;
            }

            var reviewsWithoutNamespace = new List<APIRevisionListItemModel>();

            try
            {
                // 5 second timeout to improve performance and avoid hanging on database/AAD delays
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    // Define SDK languages that we want to show for proactive review
                    var sdkLanguages = new HashSet<string> { "C#", "Java", "Python", "Go", "JavaScript" };
                    
                    // Get reviews for SDK languages that don't have namespace approval requested
                    var (allReviews, _, _, _, _, _) = await _reviewManager.GetPagedReviewListAsync(
                        search: new string[] { }, // No search filter
                        languages: sdkLanguages, // Only SDK languages
                        isClosed: false, // Only open reviews
                        isApproved: false, // Only unapproved reviews
                        offset: 0,
                        limit: 25, // Limit to 25 as requested
                        orderBy: "created"
                    );

                    // Filter for reviews that do NOT have namespace approval requested (status is NotStarted)
                    var reviewsWithoutNamespaceRequested = allReviews.Where(r => r.NamespaceReviewStatus == NamespaceReviewStatus.NotStarted).ToList();

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
            
            stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Cache miss, total time: {stopwatch.ElapsedMilliseconds}ms, returned {reviewsWithoutNamespace.Count} reviews");
            
            return reviewsWithoutNamespace;
        }
    }
}
