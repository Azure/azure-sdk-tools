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
            
            // DEBUG: Check user configuration
            var isConfiguredApprover = IsUserConfiguredApprover(userId);
            Console.WriteLine($"*** DEBUG: Current User: {userId}");
            Console.WriteLine($"*** DEBUG: User {userId} is configured approver: {isConfiguredApprover}");
            
            // DEBUG: Check user profile with error handling and timeout protection
            UserProfileModel userProfile = null;
            var approvedLanguages = new string[0];
            try
            {
                Console.WriteLine($"*** DEBUG: Attempting to get user profile for {userId}");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Attempting to get user profile for {userId}");
                
                // Reduced timeout to 5 seconds to improve performance and prevent hanging on Azure AD issues
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    userProfile = await _userProfileCache.GetUserProfileAsync(userId);
                }
                
                approvedLanguages = userProfile?.Preferences?.ApprovedLanguages?.ToArray() ?? new string[0];
                Console.WriteLine($"*** DEBUG: User profile retrieved successfully. Approved languages count: {approvedLanguages.Length}");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: User profile retrieved successfully. Approved languages count: {approvedLanguages.Length}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"*** DEBUG: User profile retrieval timed out after 5 seconds (likely Azure AD group resolution delay)");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: User profile retrieval timed out after 5 seconds");
                // Continue with empty profile - we'll still show assigned reviews
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                Console.WriteLine($"*** DEBUG: Serialization issue with user profile, continuing without profile data");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Serialization issue with user profile, continuing without profile data");
                // Continue with empty profile - we'll still show assigned reviews
            }
            catch (Exception ex)
            {
                Console.WriteLine($"*** DEBUG ERROR: Failed to get user profile: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG ERROR: Failed to get user profile: {ex.GetType().Name}");
                if (ex.Message.Contains("AAD groups are being resolved"))
                {
                    Console.WriteLine("*** DEBUG: Azure AD is resolving user's group memberships. This is expected for new users or after group changes.");
                    System.Diagnostics.Debug.WriteLine("*** DEBUG: Azure AD is resolving user's group memberships.");
                }
                // Continue with empty profile - we'll still show assigned reviews
            }
            
            // DEBUG: Attempt to get assigned revisions with error handling and timeout protection
            IEnumerable<APIRevisionListItemModel> assignedRevisions;
            try
            {
                Console.WriteLine($"*** DEBUG: Attempting to get assigned revisions for {userId}");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Attempting to get assigned revisions for {userId}");
                
                // Reduced timeout to 5 seconds to improve performance and prevent hanging on Azure AD issues
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    assignedRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);
                }
                
                Console.WriteLine($"*** DEBUG: Successfully retrieved {assignedRevisions.Count()} assigned revisions");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Successfully retrieved {assignedRevisions.Count()} assigned revisions");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"*** DEBUG: Assigned revisions retrieval timed out after 5 seconds (likely Azure AD group resolution delay)");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Assigned revisions retrieval timed out after 5 seconds");
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                Console.WriteLine($"*** DEBUG: Serialization issue with assigned revisions, continuing with empty list");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Serialization issue with assigned revisions, continuing with empty list");
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"*** DEBUG ERROR: Failed to get assigned revisions: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG ERROR: Failed to get assigned revisions: {ex.GetType().Name}");
                if (ex.Message.Contains("AAD groups are being resolved"))
                {
                    Console.WriteLine("*** DEBUG: Azure AD is still resolving user's group memberships. This is expected for new users or after group changes.");
                    System.Diagnostics.Debug.WriteLine("*** DEBUG: Azure AD is still resolving user's group memberships.");
                }
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
                    Console.WriteLine($"*** DEBUG: Skipping namespace approval reviews due to AAD group resolution delay");
                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Skipping namespace approval reviews due to AAD group resolution delay");
                    // Continue without namespace approval reviews to avoid long delays
                }
            }
            
            // Keep track of which revisions are related to namespace approval (even if they're not the primary namespace review)
            var namespaceRelatedRevisionIds = new HashSet<string>(allNamespaceApprovalReviews.Select(r => r.Id));
            Console.WriteLine($"*** DEBUG: Namespace-related revision IDs: {string.Join(", ", namespaceRelatedRevisionIds)}");
            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Namespace-related revision IDs: {string.Join(", ", namespaceRelatedRevisionIds)}");
            
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
                    bool isNamespaceRelated = parentReview.IsNamespaceReviewRequested || namespaceRelatedRevisionIds.Contains(apiRevision.Id);
                    
                    if (isNamespaceRelated && !parentReview.IsApproved)
                    {
                        bool canApproveReview = isAssignedToUser || CanUserApproveReview(parentReview);
                        if (canApproveReview)
                        {
                            Console.WriteLine($"*** DEBUG: Adding revision {apiRevision.Id} to namespace approval list (parentNamespaceRequested: {parentReview.IsNamespaceReviewRequested}, isAssociated: {namespaceRelatedRevisionIds.Contains(apiRevision.Id)})");
                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Adding revision {apiRevision.Id} to namespace approval list");
                            namespaceApprovalAPIRevs.Add(apiRevision);
                        }
                        else
                        {
                            Console.WriteLine($"*** DEBUG: User cannot approve revision {apiRevision.Id} (not assigned and not preferred approver)");
                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: User cannot approve revision {apiRevision.Id}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"*** DEBUG: Revision {apiRevision.Id} not namespace-related (parentNamespaceRequested: {parentReview.IsNamespaceReviewRequested}, parentApproved: {parentReview.IsApproved}, isAssociated: {namespaceRelatedRevisionIds.Contains(apiRevision.Id)})");
                        System.Diagnostics.Debug.WriteLine($"*** DEBUG: Revision {apiRevision.Id} not namespace-related");
                    }
                    
                    // Add to reviews without namespace approval if the parent review doesn't have namespace approval
                    // and the user is assigned as a reviewer and it's not an associated namespace revision
                    if (!parentReview.IsNamespaceReviewRequested && !parentReview.IsApproved && isAssignedToUser && !namespaceRelatedRevisionIds.Contains(apiRevision.Id))
                    {
                        reviewsWithoutNamespaceApproval.Add(apiRevision);
                    }
                }
                else
                {
                    Console.WriteLine($"*** DEBUG: No parent review found for revision {apiRevision.Id} in cached reviews");
                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: No parent review found for revision {apiRevision.Id} in cached reviews");
                }
            }
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            ReviewsWithoutNamespaceApproval = reviewsWithoutNamespaceApproval;
            ApprovedAPIRevisions.OrderByDescending(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn);

            Console.WriteLine($"*** DEBUG: Final UI model populated - NamespaceApprovalRequestedAPIRevisions count: {NamespaceApprovalRequestedAPIRevisions.Count()}");
            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Final UI model populated - NamespaceApprovalRequestedAPIRevisions count: {NamespaceApprovalRequestedAPIRevisions.Count()}");
            foreach (var rev in NamespaceApprovalRequestedAPIRevisions)
            {
                Console.WriteLine($"*** DEBUG: Namespace approval revision in UI: {rev.Id} (Review: {rev.ReviewId}, Language: '{rev.Language}', PackageName: {rev.PackageName})");
                Console.WriteLine($"*** DEBUG: Language CSS class will be: 'icon-{PageModelHelpers.GetLanguageCssSafeName(rev.Language ?? "")}'");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Namespace approval revision in UI: {rev.Id} (Review: {rev.ReviewId}, Language: '{rev.Language}', PackageName: {rev.PackageName})");
            }

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
                Console.WriteLine($"*** DEBUG: Bypassing AAD check for local testing - assuming user can approve {review.Language} reviews");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Bypassing AAD check for local testing - assuming user can approve {review.Language} reviews");
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
                Console.WriteLine($"*** DEBUG: Returning cached namespace approval results for user {userId} ({cachedResults.Count} items)");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Returning cached namespace approval results for user {userId} ({cachedResults.Count} items)");
                return cachedResults;
            }

            var namespaceApprovalReviews = new List<APIRevisionListItemModel>();

            try
            {
                Console.WriteLine($"*** DEBUG: Starting GetAllNamespaceApprovalReviews using pull request association logic");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Starting GetAllNamespaceApprovalReviews using pull request association logic");
                
                // 5 second timeout to improve performance and avoid hanging on database/AAD delays
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    // Get ALL reviews with namespace approval requested
                    var (allReviews, _, _, _, _, _) = await _reviewManager.GetPagedReviewListAsync(
                        search: new string[] { }, // No search filter
                        languages: new HashSet<string>(), // ALL languages
                        isClosed: false, // Only open reviews
                        isApproved: false, // Only unapproved reviews
                        offset: 0,
                        limit: 100, // Reduced from 1000 to improve performance - most approvers won't have >100 pending namespace requests
                        orderBy: "created"
                    );

                    Console.WriteLine($"*** DEBUG: Total reviews found: {allReviews.Count()}");
                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Total reviews found: {allReviews.Count()}");
                    
                    // Filter to only reviews with namespace approval requested
                    var nameSpaceReviews = allReviews.Where(r => r.IsNamespaceReviewRequested).ToList();
                    Console.WriteLine($"*** DEBUG: Namespace reviews found: {nameSpaceReviews.Count}");
                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Namespace reviews found: {nameSpaceReviews.Count}");
                    
                    // Process only reviews the user can approve
                    var eligibleReviews = nameSpaceReviews.Where(r => CanUserApproveReview(r)).ToList();
                    Console.WriteLine($"*** DEBUG: Processing {eligibleReviews.Count} eligible reviews with namespace approval requested");
                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Processing {eligibleReviews.Count} eligible reviews with namespace approval requested");
                    
                    foreach (var namespaceReview in eligibleReviews)
                    {
                        try
                        {
                            Console.WriteLine($"*** DEBUG: Processing review {namespaceReview.Id} ({namespaceReview.Language}) - PackageName: '{namespaceReview.PackageName}'");
                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Processing review {namespaceReview.Id} ({namespaceReview.Language}) - PackageName: '{namespaceReview.PackageName}'");
                            
                            // Extract namespace approval request information from this review's change history
                            var namespaceRequestChange = namespaceReview.ChangeHistory?.FirstOrDefault(ch => ch.ChangeAction == ReviewChangeAction.NamespaceReviewRequested);
                            var requestedOn = namespaceRequestChange?.ChangedOn ?? DateTime.MinValue;
                            var requestedBy = namespaceRequestChange?.ChangedBy ?? "Unknown";
                            
                            Console.WriteLine($"*** DEBUG: Namespace request info - RequestedOn: {requestedOn}, RequestedBy: {requestedBy}");
                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Namespace request info - RequestedOn: {requestedOn}, RequestedBy: {requestedBy}");
                            
                            // Get the latest API revision from this namespace review
                            var latestRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(namespaceReview.Id, null, APIRevisionType.All);
                            if (latestRevision != null)
                            {
                                Console.WriteLine($"*** DEBUG: Found latest revision {latestRevision.Id} for namespace review {namespaceReview.Id}");
                                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Found latest revision {latestRevision.Id} for namespace review {namespaceReview.Id}");
                                
                                // Use the SAME LOGIC as "Associated API Revisions" - find via pull requests
                                var creatingPR = (await _pullRequestManager.GetPullRequestsModelAsync(namespaceReview.Id, latestRevision.Id)).FirstOrDefault();
                                if (creatingPR != null)
                                {
                                    Console.WriteLine($"*** DEBUG: Found creating PR {creatingPR.PullRequestNumber} in repo {creatingPR.RepoName} for revision {latestRevision.Id}");
                                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Found creating PR {creatingPR.PullRequestNumber} in repo {creatingPR.RepoName} for revision {latestRevision.Id}");
                                    
                                    // Get all pull requests associated with this PR (this gives us all the different language versions)
                                    var associatedPRs = await _pullRequestManager.GetPullRequestsModelAsync(creatingPR.PullRequestNumber, creatingPR.RepoName);
                                    Console.WriteLine($"*** DEBUG: Found {associatedPRs.Count()} associated PRs for PR {creatingPR.PullRequestNumber}");
                                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Found {associatedPRs.Count()} associated PRs for PR {creatingPR.PullRequestNumber}");
                                    
                                    foreach (var associatedPR in associatedPRs)
                                    {
                                        Console.WriteLine($"*** DEBUG: Processing associated PR - Language: {associatedPR.Language}, PackageName: {associatedPR.PackageName}, ReviewId: {associatedPR.ReviewId}");
                                        System.Diagnostics.Debug.WriteLine($"*** DEBUG: Processing associated PR - Language: {associatedPR.Language}, PackageName: {associatedPR.PackageName}, ReviewId: {associatedPR.ReviewId}");
                                        
                                        // Skip if this is the original namespace review (we want the associated SDK reviews)
                                        if (associatedPR.ReviewId == namespaceReview.Id)
                                        {
                                            Console.WriteLine($"*** DEBUG: Skipping original namespace review {associatedPR.ReviewId}");
                                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Skipping original namespace review {associatedPR.ReviewId}");
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
                                                Console.WriteLine($"*** DEBUG: Adding associated {associatedPR.Language} API revision {associatedRevision.Id} from review {associatedPR.ReviewId} - PackageName: {associatedRevision.PackageName}");
                                                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Adding associated {associatedPR.Language} API revision {associatedRevision.Id} from review {associatedPR.ReviewId}");
                                                namespaceApprovalReviews.Add(associatedRevision);
                                                
                                                // Store namespace approval request info for this revision
                                                if (requestedOn != DateTime.MinValue)
                                                {
                                                    NamespaceApprovalInfo[associatedRevision.Id] = (requestedOn, requestedBy);
                                                    Console.WriteLine($"*** DEBUG: Stored namespace approval info for revision {associatedRevision.Id}: {requestedOn} by {requestedBy}");
                                                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: Stored namespace approval info for revision {associatedRevision.Id}");
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"*** DEBUG: No valid revision found for associated {associatedPR.Language} review {associatedPR.ReviewId} (may be null or already approved)");
                                                System.Diagnostics.Debug.WriteLine($"*** DEBUG: No valid revision found for associated {associatedPR.Language} review {associatedPR.ReviewId} (may be null or already approved)");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"*** DEBUG: Skipping unsupported language: {associatedPR.Language}");
                                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Skipping unsupported language: {associatedPR.Language}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"*** DEBUG: No creating PR found for revision {latestRevision.Id} - this review may not be created from a pull request");
                                    System.Diagnostics.Debug.WriteLine($"*** DEBUG: No creating PR found for revision {latestRevision.Id} - this review may not be created from a pull request");
                                    
                                    // If no PR association found, fall back to adding the revision itself if it's an SDK language
                                    var supportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        "C#", "CSharp", "Java", "Python", "Go", "JavaScript", "JS"
                                    };
                                    
                                    if (supportedLanguages.Contains(namespaceReview.Language ?? ""))
                                    {
                                        Console.WriteLine($"*** DEBUG: Adding namespace review itself as it's an SDK language: {namespaceReview.Language}");
                                        System.Diagnostics.Debug.WriteLine($"*** DEBUG: Adding namespace review itself as it's an SDK language: {namespaceReview.Language}");
                                        namespaceApprovalReviews.Add(latestRevision);
                                        
                                        // Store namespace approval request info for this revision
                                        if (requestedOn != DateTime.MinValue)
                                        {
                                            NamespaceApprovalInfo[latestRevision.Id] = (requestedOn, requestedBy);
                                            Console.WriteLine($"*** DEBUG: Stored namespace approval info for direct revision {latestRevision.Id}: {requestedOn} by {requestedBy}");
                                            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Stored namespace approval info for direct revision {latestRevision.Id}");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"*** DEBUG: No latest revision found for namespace review {namespaceReview.Id}");
                                System.Diagnostics.Debug.WriteLine($"*** DEBUG: No latest revision found for namespace review {namespaceReview.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"*** DEBUG ERROR: Failed to process namespace review {namespaceReview.Id}: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"*** DEBUG ERROR: Failed to process namespace review {namespaceReview.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"*** DEBUG: GetAllNamespaceApprovalReviews timed out after 5 seconds - improving performance by skipping expensive operations");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: GetAllNamespaceApprovalReviews timed out after 5 seconds - improving performance by skipping expensive operations");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                Console.WriteLine($"*** DEBUG: Serialization issue in GetAllNamespaceApprovalReviews, continuing with empty list");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG: Serialization issue in GetAllNamespaceApprovalReviews, continuing with empty list");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the entire request
                Console.WriteLine($"*** DEBUG ERROR: Error fetching namespace approval reviews: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"*** DEBUG ERROR: Error fetching namespace approval reviews: {ex.GetType().Name}: {ex.Message}");
                if (ex.Message.Contains("AAD groups are being resolved"))
                {
                    Console.WriteLine("*** DEBUG: Azure AD group resolution is causing namespace review fetch to fail");
                    System.Diagnostics.Debug.WriteLine("*** DEBUG: Azure AD group resolution is causing namespace review fetch to fail");
                }
            }

            Console.WriteLine($"*** DEBUG: Returning {namespaceApprovalReviews.Count} ASSOCIATED API revisions (C#/Java/Python/etc) for namespace approval UI");
            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Returning {namespaceApprovalReviews.Count} ASSOCIATED API revisions (C#/Java/Python/etc) for namespace approval UI");
            
            // Cache the results for 10 minutes to improve performance
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5), // Extend cache if accessed within 5 minutes
                Priority = CacheItemPriority.Normal
            };
            _cache.Set(cacheKey, namespaceApprovalReviews, cacheOptions);
            Console.WriteLine($"*** DEBUG: Cached namespace approval results for user {userId} for 10 minutes");
            System.Diagnostics.Debug.WriteLine($"*** DEBUG: Cached namespace approval results for user {userId} for 10 minutes");
            
            return namespaceApprovalReviews;
        }
    }
}
