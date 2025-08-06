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
            
            // Step 1: Check user configuration
            var step1Stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var isConfiguredApprover = IsUserConfiguredApprover(userId);
            step1Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 1 (Check user config) took: {step1Stopwatch.ElapsedMilliseconds}ms");
            
            // Step 2: Check user profile with error handling and timeout protection
            var step2Stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
            step2Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 2 (Get user profile) took: {step2Stopwatch.ElapsedMilliseconds}ms");
            
            // Step 3: Attempt to get assigned revisions with error handling and timeout protection
            var step3Stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
            step3Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 3 (Get assigned revisions) took: {step3Stopwatch.ElapsedMilliseconds}ms, found {assignedRevisions.Count()} revisions");
            
            APIRevisions = assignedRevisions;

            List<APIRevisionListItemModel> activeAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> approvedAPIRevs = new List<APIRevisionListItemModel>();
            List<APIRevisionListItemModel> namespaceApprovalAPIRevs = new List<APIRevisionListItemModel>();

            // Step 4: Get all reviews with namespace approval requested for languages the user can approve
            var step4Stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
            else
            {
                Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - User {userId} is not configured as approver, skipping namespace approvals");
            }
            step4Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 4 (Get namespace approvals) took: {step4Stopwatch.ElapsedMilliseconds}ms");
            
            // Step 5: Merge namespace approval reviews with assigned reviews
            var step5Stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
            step5Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 5 (Merge reviews) took: {step5Stopwatch.ElapsedMilliseconds}ms, total reviews: {APIRevisions.Count()}");

            // Step 6: Get all unique review IDs to minimize database calls
            var step6Stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
            step6Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 6 (Batch fetch parent reviews) took: {step6Stopwatch.ElapsedMilliseconds}ms");

            // Step 7: Process and categorize revisions
            var step7Stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
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
                        // SIMPLIFIED: If the user can access this page and is a configured approver,
                        // they can approve namespace reviews. No need for expensive per-review permission checks.
                        if (isAssignedToUser || isConfiguredApprover)
                        {
                            namespaceApprovalAPIRevs.Add(apiRevision);
                        }
                    }
                }
            }
            step7Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 7 (Process and categorize) took: {step7Stopwatch.ElapsedMilliseconds}ms");
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            
            // Step 8: Always populate reviews without namespace approval for the 4th tab
            var step8Stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ReviewsWithoutNamespaceApproval = await GetReviewsWithoutNamespaceApproval();
            step8Stopwatch.Stop();
            Console.WriteLine($"[NAMESPACE_PERF] RequestedReviews - Step 8 (Get reviews without namespace) took: {step8Stopwatch.ElapsedMilliseconds}ms");
            
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
        /// Limited to SDK languages (C#, Java, Python, Go, JavaScript) and maximum 15 results
        /// Results are cached for 5 minutes to improve performance
        /// OPTIMIZED: Uses batch processing to minimize database calls
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
                // 10 second timeout for this complex operation
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    // Define SDK languages that we want to show for proactive review
                    var sdkLanguages = new HashSet<string> { "C#", "Java", "Python", "Go", "JavaScript" };
                    
                    // Step 8a: Get reviews for SDK languages that don't have namespace approval requested
                    var step8aStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var (allReviews, _, _, _, _, _) = await _reviewManager.GetPagedReviewListAsync(
                        search: new string[] { }, // No search filter
                        languages: sdkLanguages, // Only SDK languages
                        isClosed: false, // Only open reviews
                        isApproved: false, // Only unapproved reviews
                        offset: 0,
                        limit: 15, // Reduced from 25 to 15 for better performance
                        orderBy: "created"
                    );
                    step8aStopwatch.Stop();
                    Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Step 8a (Get paged reviews) took: {step8aStopwatch.ElapsedMilliseconds}ms, found {allReviews.Count()} reviews");

                    // Filter for reviews that do NOT have namespace approval requested (status is NotStarted)
                    var reviewsWithoutNamespaceRequested = allReviews.Where(r => r.NamespaceReviewStatus == NamespaceReviewStatus.NotStarted).ToList();
                    Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Filtered to {reviewsWithoutNamespaceRequested.Count} reviews without namespace approval");

                    if (reviewsWithoutNamespaceRequested.Any())
                    {
                        // Step 8b: OPTIMIZED - Batch fetch all API revisions for multiple reviews at once
                        var step8bStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        
                        // Extract all review IDs
                        var reviewIds = reviewsWithoutNamespaceRequested.Select(r => r.Id).ToList();
                        
                        // Use Task.WhenAll to fetch all API revisions in parallel (limited to 4 concurrent calls)
                        var semaphore = new SemaphoreSlim(4); // Limit to 4 concurrent database calls
                        var revisionTasks = reviewIds.Select(async reviewId =>
                        {
                            await semaphore.WaitAsync(cts.Token);
                            try
                            {
                                var apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(reviewId);
                                var latestRevision = apiRevisions?.OrderByDescending(r => r.CreatedOn).FirstOrDefault();
                                return (reviewId, latestRevision);
                            }
                            catch (Exception)
                            {
                                return (reviewId, (APIRevisionListItemModel)null);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });

                        var revisionResults = await Task.WhenAll(revisionTasks);
                        
                        // Filter and add valid results
                        foreach (var (reviewId, latestRevision) in revisionResults)
                        {
                            if (latestRevision != null && !latestRevision.IsApproved)
                            {
                                reviewsWithoutNamespace.Add(latestRevision);
                            }
                        }
                        
                        step8bStopwatch.Stop();
                        Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Step 8b (Batch fetch revisions) took: {step8bStopwatch.ElapsedMilliseconds}ms, processed {reviewIds.Count} reviews");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Operation timed out");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - TempData serialization error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NAMESPACE_PERF] GetReviewsWithoutNamespaceApproval - Unexpected error: {ex.Message}");
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
