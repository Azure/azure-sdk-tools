using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.DIff;
using APIView.Model;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;


namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private static int REVIEW_DIFF_CONTEXT_SIZE = 3;
        private const string DIFF_CONTEXT_SEPERATOR = "<br><span>.....</span><br>";
        private readonly IReviewManager _manager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly ICommentsManager _commentsManager;
        private readonly INotificationManager _notificationManager;
        public readonly UserPreferenceCache _preferenceCache;
        private readonly ICosmosUserProfileRepository _userProfileRepository;
        private readonly IConfiguration _configuration;

        private readonly IHubContext<SignalRHub> _signalRHubContext;

        public ReviewPageModel(
            IReviewManager manager,
            IPullRequestManager pullRequestManager,
            IBlobCodeFileRepository codeFileRepository,
            ICommentsManager commentsManager,
            INotificationManager notificationManager,
            UserPreferenceCache preferenceCache,
            ICosmosUserProfileRepository userProfileRepository,
            IConfiguration configuration,
            IHubContext<SignalRHub> signalRHub)
        {
            _manager = manager;
            _pullRequestManager = pullRequestManager;
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _notificationManager = notificationManager;
            _preferenceCache = preferenceCache;
            _userProfileRepository = userProfileRepository;
            _configuration = configuration;
            _signalRHubContext = signalRHub;
        }

        public ReviewModel Review { get; set; }
        public ReviewRevisionModel Revision { get; set; }
        public ReviewRevisionModel DiffRevision { get; set; }
        public ReviewRevisionModel[] PreviousRevisions {get; set; }
        public CodeFile CodeFile { get; set; }
        public CodeLineModel[] Lines { get; set; }
        public InlineDiffLine<CodeLine>[] DiffLines { get; set; }
        public ReviewCommentsModel Comments { get; set; }
        public HashSet<GithubUser> TaggableUsers { get; set; }
        public HashSet<int> HeadingsOfSectionsWithDiff { get; set; } = new HashSet<int>();

        /// <summary>
        /// The number of active conversations for this iteration
        /// </summary>
        public int ActiveConversations { get; set; }

        public int TotalActiveConversations { get; set; }

        public int UsageSampleConversations { get; set; }

        [BindProperty(SupportsGet = true)]
        public string DiffRevisionId { get; set; }

        // Flag to decide whether to  include documentation
        [BindProperty(Name = "doc", SupportsGet = true)]
        public bool ShowDocumentation { get; set; }

        [BindProperty(Name = "diffOnly", SupportsGet = true)]
        public bool ShowDiffOnly { get; set; }

        public IEnumerable<ReviewModel> ReviewsForPackage { get; set; } = new List<ReviewModel>();

        public readonly HashSet<string> PreferredApprovers = new HashSet<string>();

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "api";

            await GetReviewPageModelPropertiesAsync(id, revisionId);

            if (!Review.Revisions.Any())
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }
            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            CodeFile = renderedCodeFile.CodeFile;

            var fileDiagnostics = CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            var fileHtmlLines = renderedCodeFile.Render(ShowDocumentation);

            if (DiffRevision != null)
            {
                var previousRevisionFile = await _codeFileRepository.GetCodeFileAsync(DiffRevision);

                var previousHtmlLines = previousRevisionFile.RenderReadOnly(ShowDocumentation);
                var previousRevisionTextLines = previousRevisionFile.RenderText(ShowDocumentation);
                var fileTextLines = renderedCodeFile.RenderText(ShowDocumentation);

                var diffLines = InlineDiff.Compute(
                    previousRevisionTextLines,
                    fileTextLines,
                    previousHtmlLines,
                    fileHtmlLines);

                Lines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: diffLines, 
                    comments: Comments, showDiffOnly: ShowDiffOnly, reviewDiffContextSize: REVIEW_DIFF_CONTEXT_SIZE,
                    diffContextSeparator: DIFF_CONTEXT_SEPERATOR, headingsOfSectionsWithDiff: HeadingsOfSectionsWithDiff);
                if (Lines.Length == 0)
                {
                    var notifcation = new NotificationModel() { Message = "There is no diff between the two revisions.", Level = NotificatonLevel.Info };
                    await _signalRHubContext.Clients.Group(User.GetGitHubLogin()).SendAsync("RecieveNotification", notifcation);
                    return Redirect(Request.Headers["referer"]);
                }
            }
            else
            {
                Lines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: fileHtmlLines, comments: Comments);
            }

            ActiveConversations = PageModelHelpers.ComputeActiveConversations(lines: fileHtmlLines, comments: Comments);
            TotalActiveConversations = Comments.Threads.Count(t => !t.IsResolved);
            UsageSampleConversations = Comments.Threads.Count(t => t.Comments.FirstOrDefault()?.IsUsageSampleComment == true);
            var filterPreference = _preferenceCache.GetFilterType(User.GetGitHubLogin(), Review.FilterType);
            ReviewsForPackage = await _manager.GetReviewsAsync(Review.ServiceName, Review.PackageDisplayName, filterPreference);

            var approverConfig = _configuration["approvers"];
            if (!string.IsNullOrEmpty(approverConfig))
            {
                foreach (var username in approverConfig.Split(","))
                {
                    if (username.Equals(User.GetGitHubLogin()))
                    {
                        var userCache = _preferenceCache.GetUserPreferences(User).Result;
                        var langs = userCache.ApprovedLanguages.ToHashSet();
                        if (!langs.Any())
                        {
                            UserProfileModel user = await _userProfileRepository.TryGetUserProfileAsync(username);
                            langs = user.Languages;
                            userCache.ApprovedLanguages = langs;
                            _preferenceCache.UpdateUserPreference(userCache, User);
                        }
                        if (langs.Contains(Review.Language) || !langs.Any())
                        {
                            PreferredApprovers.Add(username);
                        }
                    }
                    else
                    {
                        UserProfileModel user = await _userProfileRepository.TryGetUserProfileAsync(username);
                        var langs = user.Languages;
                        if (langs.Contains(Review.Language) || !langs.Any())
                        {
                            PreferredApprovers.Add(username);
                        }
                    }
                }
            }

            return Page();
        }

        public async Task<PartialViewResult> OnGetCodeLineSectionAsync(
            string id, int sectionKey, int? sectionKeyA = null, int? sectionKeyB = null,
            string revisionId = null, string diffRevisionId = null, bool diffOnly = false)
        {
            await GetReviewPageModelPropertiesAsync(id, revisionId, diffRevisionId, diffOnly);
            var renderedCodeFile = await _codeFileRepository.GetCodeFileAsync(Revision);
            var fileDiagnostics = renderedCodeFile.CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            CodeLine[] currentHtmlLines;
            var userPrefernce = await _preferenceCache.GetUserPreferences(User) ?? new UserPreferenceModel();

            if (DiffRevision != null)
            {
                InlineDiffLine<CodeLine>[] diffLines;
                var previousRevisionFile = await _codeFileRepository.GetCodeFileAsync(DiffRevision);

                if (sectionKeyA != null && sectionKeyB != null)
                {
                    var currentRootNode = renderedCodeFile.GetCodeLineSectionRoot((int)sectionKeyA);
                    var previousRootNode = previousRevisionFile.GetCodeLineSectionRoot((int)sectionKeyB);
                    var diffSectionRoot = _manager.ComputeSectionDiff(previousRootNode, currentRootNode, previousRevisionFile, renderedCodeFile);
                    diffLines = renderedCodeFile.GetDiffCodeLineSection(diffSectionRoot);
                }
                else if (sectionKeyA != null)
                {
                    currentHtmlLines = renderedCodeFile.GetCodeLineSection((int)sectionKeyA);
                    var previousRevisionHtmlLines = new CodeLine[] { };
                    var previousRevisionTextLines = new CodeLine[] { };
                    var currentRevisionTextLines = renderedCodeFile.GetCodeLineSection((int)sectionKeyA, renderType: RenderType.Text);
                    diffLines = InlineDiff.Compute(
                        previousRevisionTextLines,
                        currentRevisionTextLines,
                        previousRevisionHtmlLines,
                        currentHtmlLines);
                }
                else 
                {
                    currentHtmlLines = new CodeLine[] { }; 
                    var previousRevisionHtmlLines = previousRevisionFile.GetCodeLineSection((int)sectionKeyB, RenderType.ReadOnly);
                    var previousRevisionTextLines = previousRevisionFile.GetCodeLineSection((int)sectionKeyB, renderType: RenderType.Text);
                    var currentRevisionTextLines = new CodeLine[] { };
                    diffLines = InlineDiff.Compute(
                        previousRevisionTextLines,
                        currentRevisionTextLines,
                        previousRevisionHtmlLines,
                        currentHtmlLines);
                }
                Lines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: diffLines, comments: Comments,
                    showDiffOnly: ShowDiffOnly, reviewDiffContextSize: REVIEW_DIFF_CONTEXT_SIZE,
                    diffContextSeparator: DIFF_CONTEXT_SEPERATOR, headingsOfSectionsWithDiff: HeadingsOfSectionsWithDiff, hideCommentRows: true);
            }
            else
            {
                currentHtmlLines = renderedCodeFile.GetCodeLineSection(sectionKey);
                Lines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: currentHtmlLines, comments: Comments, hideCommentRows: true);
            }
            TempData["CodeLineSection"] = Lines;
            TempData["UserPreference"] = userPrefernce;
            return Partial("_CodeLinePartial", sectionKey);
        }

        public async Task<ActionResult> OnPostToggleClosedAsync(string id)
        {
            await _manager.ToggleIsClosedAsync(User, id);

            return RedirectToPage(new { id = id });
        }

        public async Task<ActionResult> OnPostToggleSubscribedAsync(string id)
        {
            await _notificationManager.ToggleSubscribedAsync(User, id);
            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostToggleApprovalAsync(string id, string revisionId)
        {
            await _manager.ToggleApprovalAsync(User, id, revisionId);
            return RedirectToPage(new { id = id });
        }

        public async Task<ActionResult> OnPostRequestReviewersAsync(string id, HashSet<string> reviewers)
        {
            await _manager.RequestApproversAsync(User, id, reviewers);
            await _notificationManager.NotifyApproversOfReview(User, id, reviewers);
            return RedirectToPage(new { id = id });
        }

        public Dictionary<string, string> GetRoutingData(string diffRevisionId = null, bool? showDiffOnly = null, bool? showDocumentation = null, string revisionId = null)
        {
            var routingData = new Dictionary<string, string>();
            routingData["revisionId"] = revisionId;
            routingData["diffRevisionId"] = diffRevisionId;
            routingData["doc"] = (showDocumentation ?? false).ToString();
            routingData["diffOnly"] = (showDiffOnly ?? false).ToString();
            return routingData;
        }

        public async Task<IEnumerable<PullRequestModel>> GetAssociatedPullRequest()
        {
            return await _pullRequestManager.GetPullRequestsModel(Review.ReviewId);
        }

        public async Task<IEnumerable<PullRequestModel>> GetPRsOfAssoicatedReviews()
        {
            var creatingPR = (await _pullRequestManager.GetPullRequestsModel(Review.ReviewId)).FirstOrDefault();
            return await _pullRequestManager.GetPullRequestsModel(creatingPR.PullRequestNumber, creatingPR.RepoName);;
        }

        private async Task GetReviewPageModelPropertiesAsync(string id, string revisionId = null, string diffRevisionId = null, bool diffOnly = false)
        {
            Review = await _manager.GetReviewAsync(User, id);
            TaggableUsers = _commentsManager.GetTaggableUsers();
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            Revision = Review.Revisions.Last();
            if (revisionId != null) 
            {
                var revision = Review.Revisions.Where(r => r.RevisionId == revisionId);
                if (revision.Count() == 1)
                {
                    Revision = revision.Single();
                }
                else 
                {
                    var notifcation = new NotificationModel() { Message = $"A revision with ID {revisionId} does not exist for this review.", Level = NotificatonLevel.Warning };
                    await _signalRHubContext.Clients.Group(User.GetGitHubLogin()).SendAsync("RecieveNotification", notifcation);
                }

            }
                
            PreviousRevisions = Review.Revisions.TakeWhile(r => r != Revision).ToArray();
            DiffRevisionId = (DiffRevisionId == null) ? diffRevisionId : DiffRevisionId;
            ShowDiffOnly = (ShowDiffOnly == false) ? diffOnly : ShowDiffOnly;
            DiffRevision = DiffRevisionId != null ?
                PreviousRevisions.Single(r => r.RevisionId == DiffRevisionId) :
                DiffRevision;
            HeadingsOfSectionsWithDiff = (DiffRevision != null && DiffRevision.HeadingsOfSectionsWithDiff.ContainsKey(Revision.RevisionId)) ? 
                DiffRevision.HeadingsOfSectionsWithDiff[Revision.RevisionId] : new HashSet<int>();
        }
    }
}
