using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using APIView.DIff;
using ApiView;
using APIView;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.LeanModels;
using System.Threading.Tasks;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using APIViewWeb.Pages.Assemblies;

namespace APIViewWeb.Helpers
{
    public static class PageModelHelpers
    {
        public static UserPreferenceModel GetUserPreference(UserProfileCache userProfileCache, ClaimsPrincipal User)
        {
            return userProfileCache.GetUserProfileAsync(User.GetGitHubLogin()).Result.Preferences;
        }

        public static string GetHiddenApiClass(UserPreferenceModel userPreference)
        {
            var hiddenApiClass = " hidden-api hidden-api-toggleable";
            if (userPreference.ShowHiddenApis != true)
            {
                hiddenApiClass += " d-none";
            }
            return hiddenApiClass;
        }

        public static string GetLanguageCssSafeName(string language)
        {
            switch (language.ToLower())
            {
                case "c#":
                    return "csharp";
                case "c++":
                    return "cplusplus";
                default:
                    return language.ToLower();
            }
        }

        public static string GetUserEmail(ClaimsPrincipal user) => NotificationManager.GetUserEmail(user);

        public static bool IsUserSubscribed(ClaimsPrincipal user, HashSet<string> subscribers)
        {
            string email = GetUserEmail(user);
            if (email != null)
            {
                return subscribers.Contains(email);
            }
            return false;
        }

        /// <summary>
        /// Create the CodelIneModel from Diffs
        /// </summary>
        /// <param name="diagnostics"></param>
        /// <param name="lines"></param>
        /// <param name="comments"></param>
        /// <param name="showDiffOnly"></param>
        /// <param name="reviewDiffContextSize"></param>
        /// <param name="diffContextSeparator"></param>
        /// <param name="headingsOfSectionsWithDiff"></param>
        /// <param name="hideCommentRows"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public static CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, InlineDiffLine<CodeLine>[] lines,
            ReviewCommentsModel comments, bool showDiffOnly, int reviewDiffContextSize, string diffContextSeparator,
            HashSet<int> headingsOfSectionsWithDiff, bool hideCommentRows = false, string language = null)
        {
            if (showDiffOnly)
            {
                lines = CreateDiffOnlyLines(lines, reviewDiffContextSize, diffContextSeparator);
                if (lines.Length == 0)
                {
                    return Array.Empty<CodeLineModel>();
                }
            }
            List<int> documentedByLines = new List<int>();
            int lineNumberExcludingDocumentation = 0;
            int diffSectionId = 0;

            return lines.Select(
                (diffLine, index) =>
                {
                    if (diffLine.Line.IsDocumentation)
                    {
                        // documentedByLines must include the index of a line, assuming that documentation lines are counted
                        documentedByLines.Add(++index);
                        return new CodeLineModel(
                            kind: diffLine.Kind,
                            codeLine: diffLine.Line,
                            commentThread: comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread, hideCommentRows) ?
                                thread :
                                null,
                            diagnostics: diffLine.Kind != DiffLineKind.Removed ?
                                diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                                Array.Empty<CodeDiagnostic>(),
                            lineNumber: lineNumberExcludingDocumentation,
                            documentedByLines: new int[] { },
                            isDiffView: true,
                            diffSectionId: diffLine.Line.SectionKey != null ? ++diffSectionId : null,
                            otherLineSectionKey: diffLine.Kind == DiffLineKind.Unchanged ? diffLine.OtherLine.SectionKey : null,
                            headingsOfSectionsWithDiff: headingsOfSectionsWithDiff,
                            isSubHeadingWithDiffInSection: diffLine.IsHeadingWithDiffInSection,
                            language: language
                        );
                    }
                    else
                    {
                        CodeLineModel c = new CodeLineModel(
                             kind: diffLine.Kind,
                             codeLine: diffLine.Line,
                             commentThread: diffLine.Kind != DiffLineKind.Removed &&
                                 comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread, hideCommentRows) ?
                                     thread :
                                     null,
                             diagnostics: diffLine.Kind != DiffLineKind.Removed ?
                                 diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                                 Array.Empty<CodeDiagnostic>(),
                             lineNumber: diffLine.Line.LineNumber ?? ++lineNumberExcludingDocumentation,
                             documentedByLines: documentedByLines.ToArray(),
                             isDiffView: true,
                             diffSectionId: diffLine.Line.SectionKey != null ? ++diffSectionId : null,
                             otherLineSectionKey: diffLine.Kind == DiffLineKind.Unchanged ? diffLine.OtherLine.SectionKey : null,
                             headingsOfSectionsWithDiff: headingsOfSectionsWithDiff,
                             isSubHeadingWithDiffInSection: diffLine.IsHeadingWithDiffInSection,
                             language: language
                         );
                        documentedByLines.Clear();
                        return c;
                    }
                }).ToArray();
        }

        /// <summary>
        /// Create CodeLineModel fron regular codelines
        /// </summary>
        /// <param name="diagnostics"></param>
        /// <param name="lines"></param>
        /// <param name="comments"></param>
        /// <param name="hideCommentRows"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public static CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, CodeLine[] lines, ReviewCommentsModel comments, bool hideCommentRows = false, string language = null)
        {
            List<int> documentedByLines = new List<int>();
            int lineNumberExcludingDocumentation = 0;
            return lines.Select(
                (line, index) =>
                {
                    if (line.IsDocumentation)
                    {
                        // documentedByLines must include the index of a line, assuming that documentation lines are counted
                        documentedByLines.Add(++index);
                        return new CodeLineModel(
                            kind: DiffLineKind.Unchanged,
                            codeLine: line,
                            commentThread: comments.TryGetThreadForLine(line.ElementId, out var thread, hideCommentRows) ? thread : null,
                            diagnostics: diagnostics.Where(d => d.TargetId == line.ElementId).ToArray(),
                            lineNumber: lineNumberExcludingDocumentation,
                            documentedByLines: new int[] { },
                            language: language

                        );
                    }
                    else
                    {
                        CodeLineModel c = new CodeLineModel(
                            kind: DiffLineKind.Unchanged,
                            codeLine: line,
                            commentThread: comments.TryGetThreadForLine(line.ElementId, out var thread, hideCommentRows) ? thread : null,
                            diagnostics: diagnostics.Where(d => d.TargetId == line.ElementId).ToArray(),
                            lineNumber: line.LineNumber ?? ++lineNumberExcludingDocumentation,
                            documentedByLines: documentedByLines.ToArray(),
                            language: language
                        );
                        documentedByLines.Clear();
                        return c;
                    }
                }).ToArray();
        }

        /// <summary>
        /// Compute conversiation info in the review
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="comments"></param>
        /// <returns></returns>
        public static int ComputeActiveConversationsInActiveRevision(CodeLine[] lines, ReviewCommentsModel comments)
        {
            int activeThreadsFromActiveReviewRevisions = 0;

            foreach (CodeLine line in lines)
            {
                if (string.IsNullOrEmpty(line.ElementId))
                {
                    continue;
                }

                // if we have comments for this line and the thread has not been resolved.
                // Add "&& !thread.Comments.First().IsUsageSampleComment()" to exclude sample comments from being counted (This also prevents the popup before approval)
                if (comments.TryGetThreadForLine(line.ElementId, out CommentThreadModel reviewThread) && !reviewThread.IsResolved && reviewThread.Comments.First().CommentType == CommentType.APIRevision)
                {
                    activeThreadsFromActiveReviewRevisions++;
                }
            }
            return activeThreadsFromActiveReviewRevisions;
        }


        /// <summary>
        /// Get all the data needed to for a review page
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="reviewManager"></param>
        /// <param name="userProfileCache"></param>
        /// <param name="reviewRevisionsManager"></param> 
        /// <param name="commentManager"></param>
        /// <param name="codeFileRepository"></param>
        /// <param name="signalRHubContext"></param>
        /// <param name="permissionsManager"></param>
        /// <param name="user"></param>
        /// <param name="review"></param>
        /// <param name="reviewId"></param>
        /// <param name="revisionId"></param>
        /// <param name="diffRevisionId"></param>
        /// <param name="showDocumentation"></param>
        /// <param name="showDiffOnly"></param>
        /// <param name="diffContextSize"></param>
        /// <param name="diffContextSeperator"></param>
        /// <returns></returns>
        public static async Task<ReviewContentModel> GetReviewContentAsync(
            IConfiguration configuration, IReviewManager reviewManager, UserProfileCache userProfileCache,
            IAPIRevisionsManager reviewRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, IHubContext<SignalRHub> signalRHubContext, IPermissionsManager permissionsManager, ClaimsPrincipal user, ReviewListItemModel review = null, string reviewId = null,
            string revisionId = null, string diffRevisionId = null, bool showDocumentation = false, bool showDiffOnly = false, int diffContextSize = 3,
            string diffContextSeperator = "<br><span>.....</span><br>")
        {
            var reviewPageContent = new ReviewContentModel()
            {
                Directive = ReviewContentModelDirective.ProceedWithPageLoad
            };

            var userId = user.GetGitHubLogin();
            if (review == null)
            {
                review = await reviewManager.GetReviewAsync(user, reviewId);
            }
           
            if (review == null)
            {
                reviewPageContent.Directive = ReviewContentModelDirective.TryGetlegacyReview;
                return reviewPageContent;
            }

            var apiRevisions = await reviewRevisionsManager.GetAPIRevisionsAsync(review.Id);

            // Try getting latest Automatic Revision, otherwise get latest of any type or default
            var activeRevision = await reviewRevisionsManager.GetLatestAPIRevisionsAsync(review.Id, apiRevisions, APIRevisionType.Automatic);
            if (activeRevision == null)
            {
                reviewPageContent.Directive = ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonRedirectToIndexPage;
                reviewPageContent.NotificationMessage = $"Review with ID {review.Id} has no valid APIRevisions";
                return reviewPageContent;
            }

            APIRevisionListItemModel diffRevision = null;
            if (!string.IsNullOrEmpty(revisionId)) {
                if (apiRevisions.Where(x => x.Id == revisionId).Any())
                {
                    activeRevision = apiRevisions.First(x => x.Id == revisionId);
                }
                else
                {
                    reviewPageContent.NotificationMessage = $"A revision with ID {revisionId} does not exist for review with id {review.Id}";
                    reviewPageContent.Directive = ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonRedirectToIndexPage;
                    return reviewPageContent;
                }
            }

            if (activeRevision.Files[0].ParserStyle == ParserStyle.Tree)
            {
                reviewPageContent.Directive = ReviewContentModelDirective.RedirectToSPAUI;
                reviewPageContent.ActiveAPIRevision = activeRevision;
                return reviewPageContent;
            }

            var comments = await commentManager.GetReviewCommentsAsync(review.Id);
            var activeRevisionRenderableCodeFile = await codeFileRepository.GetCodeFileAsync(activeRevision.Id, activeRevision.Files[0], activeRevision.Language);
            var activeRevisionReviewCodeFile = activeRevisionRenderableCodeFile.CodeFile;
            var fileDiagnostics = activeRevisionReviewCodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            var activeRevisionHtmlLines = activeRevisionRenderableCodeFile.Render(showDocumentation: showDocumentation);
            var codeLines = new CodeLineModel[0];
            var getCodeLines = false;



            if (!string.IsNullOrEmpty(diffRevisionId))
            {
                if (apiRevisions.Where(x => x.Id == diffRevisionId).Any())
                {
                    diffRevision = await reviewRevisionsManager.GetAPIRevisionAsync(user, diffRevisionId);
                    var diffRevisionRenderableCodeFile = await codeFileRepository.GetCodeFileAsync(diffRevisionId, diffRevision.Files[0], activeRevision.Language);
                    var diffRevisionHTMLLines = diffRevisionRenderableCodeFile.RenderReadOnly(showDocumentation: showDocumentation);
                    var diffRevisionTextLines = diffRevisionRenderableCodeFile.RenderText(showDocumentation: showDocumentation);

                    var activeRevisionTextLines = activeRevisionRenderableCodeFile.RenderText(showDocumentation: showDocumentation);

                    var diffLines = InlineDiff.Compute(diffRevisionTextLines, activeRevisionTextLines, diffRevisionHTMLLines, activeRevisionHtmlLines);
                    var headingsOfSectionsWithDiff = activeRevision.HeadingsOfSectionsWithDiff.ContainsKey(diffRevision.Id) ? activeRevision.HeadingsOfSectionsWithDiff[diffRevision.Id] : new HashSet<int>();

                    codeLines = CreateLines(diagnostics: fileDiagnostics, lines: diffLines, comments: comments, showDiffOnly: showDiffOnly,
                        reviewDiffContextSize: diffContextSize, diffContextSeparator: diffContextSeperator, headingsOfSectionsWithDiff: headingsOfSectionsWithDiff,
                        language: activeRevision.Language);

                    if (!codeLines.Any())
                    {
                        getCodeLines = true;
                        reviewPageContent.NotificationMessage = $"There is no diff between the two revisions. {activeRevision.Id} : {diffRevisionId}";
                        reviewPageContent.Directive = ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonProceedWithPageLoad;
                    }
                }
                else
                {
                    getCodeLines = true;
                    reviewPageContent.NotificationMessage = $"A diffRevision with ID {diffRevisionId} does not exist for this review.";
                    reviewPageContent.Directive = ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonProceedWithPageLoad;
                }
            }

            if (string.IsNullOrEmpty(diffRevisionId) || getCodeLines)
            {
                codeLines = CreateLines(diagnostics: fileDiagnostics, lines: activeRevisionHtmlLines, comments: comments, language: activeRevision.Language);
            }

            if (codeLines == null || codeLines.Length == 0)
            {
                reviewPageContent.NotificationMessage = $"A revision with ID {activeRevision.Id} has no content.";
                reviewPageContent.Directive = ReviewContentModelDirective.ErrorDueToInvalidAPIRevisonRedirectToIndexPage;
                return reviewPageContent;
            }

            reviewPageContent.codeLines = codeLines;
            reviewPageContent.ActiveConversationsInActiveAPIRevision = ComputeActiveConversationsInActiveRevision(activeRevisionHtmlLines, comments);

            HashSet<string> languageApprovers = await permissionsManager.GetApproversForLanguageAsync(review.Language);

            reviewPageContent.Review = review;
            reviewPageContent.Navigation = activeRevisionRenderableCodeFile.CodeFile.Navigation;
            reviewPageContent.APIRevisions = apiRevisions.OrderByDescending(c => c.CreatedOn);
            reviewPageContent.ActiveAPIRevision = activeRevision;
            reviewPageContent.DiffAPIRevision = diffRevision;
            reviewPageContent.TotalActiveConversations = comments.Threads.Count(t => !t.IsResolved);
            reviewPageContent.HasFatalDiagnostics = fileDiagnostics.Any(d => d.Level == APIView.CodeDiagnosticLevel.Fatal);
            reviewPageContent.ActiveConversationsInActiveAPIRevision = ComputeActiveConversationsInActiveRevision(activeRevisionHtmlLines, comments);
            reviewPageContent.ActiveConversationsInSampleRevisions = comments.Threads.Count(t => t.Comments.FirstOrDefault()?.CommentType == CommentType.SampleRevision);
            reviewPageContent.LanguageApprovers = languageApprovers;
            reviewPageContent.TaggableUsers = commentManager.GetTaggableUsers();
            reviewPageContent.PageHasLoadableSections = activeRevisionReviewCodeFile.LeafSections?.Any() ?? false;

            return reviewPageContent;
        }

        /// <summary>
        /// Get CodeLineSection
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewManager"></param>
        /// <param name="apiRevisionsManager"></param>
        /// <param name="commentManager"></param>
        /// <param name="codeFileRepository"></param>
        /// <param name="reviewId"></param>
        /// <param name="sectionKey"></param>
        /// <param name="revisionId"></param>
        /// <param name="diffRevisionId"></param>
        /// <param name="diffContextSize"></param>
        /// <param name="diffContextSeperator"></param>
        /// <param name="sectionKeyA"></param>
        /// <param name="sectionKeyB"></param>
        /// <returns></returns>
        public static async Task<CodeLineModel[]> GetCodeLineSectionAsync(ClaimsPrincipal user, IReviewManager reviewManager,
            IAPIRevisionsManager apiRevisionsManager, ICommentsManager commentManager,
            IBlobCodeFileRepository codeFileRepository, string reviewId, int sectionKey, string revisionId = null,
            string diffRevisionId = null, int diffContextSize = 3, string diffContextSeperator = "<br><span>.....</span><br>",
             int? sectionKeyA = null, int? sectionKeyB = null
            )
        {
            var activeRevision = await apiRevisionsManager.GetAPIRevisionAsync(user, revisionId);
            var activeRevisionRenderableCodeFile = await codeFileRepository.GetCodeFileAsync(activeRevision.Id, activeRevision.Files[0], activeRevision.Language);
            var fileDiagnostics = activeRevisionRenderableCodeFile.CodeFile.Diagnostics ?? Array.Empty<CodeDiagnostic>();
            CodeLine[] activeRevisionHTMLLines;

            var comments = await commentManager.GetReviewCommentsAsync(reviewId);

            var codeLines = new CodeLineModel[0];

            if (diffRevisionId != null)
            {
                InlineDiffLine<CodeLine>[] diffLines;
                var diffRevision = await apiRevisionsManager.GetAPIRevisionAsync(user, diffRevisionId);
                var diffRevisionRenderableCodeFile = await codeFileRepository.GetCodeFileAsync(diffRevisionId, diffRevision.Files[0], activeRevision.Language);

                if (sectionKeyA != null && sectionKeyB != null)
                {
                    var currentRootNode = activeRevisionRenderableCodeFile.GetCodeLineSectionRoot((int)sectionKeyA);
                    var previousRootNode = diffRevisionRenderableCodeFile.GetCodeLineSectionRoot((int)sectionKeyB);
                    var diffSectionRoot = apiRevisionsManager.ComputeSectionDiff(previousRootNode, currentRootNode, diffRevisionRenderableCodeFile, activeRevisionRenderableCodeFile);
                    diffLines = activeRevisionRenderableCodeFile.GetDiffCodeLineSection(diffSectionRoot);
                }
                else if (sectionKeyA != null)
                {
                    activeRevisionHTMLLines = activeRevisionRenderableCodeFile.GetCodeLineSection((int)sectionKeyA);
                    var diffRevisionHtmlLines = new CodeLine[] { };
                    var diffRevisionTextLines = new CodeLine[] { };
                    var activeRevisionTextLines = activeRevisionRenderableCodeFile.GetCodeLineSection((int)sectionKeyA, renderType: RenderType.Text);
                    diffLines = InlineDiff.Compute(
                        diffRevisionTextLines,
                        activeRevisionTextLines,
                        diffRevisionHtmlLines,
                        activeRevisionHTMLLines);
                }
                else
                {
                    activeRevisionHTMLLines = new CodeLine[] { };
                    var diffRevisionHtmlLines = diffRevisionRenderableCodeFile.GetCodeLineSection((int)sectionKeyB, RenderType.ReadOnly);
                    var diffRevisionTextLines = diffRevisionRenderableCodeFile.GetCodeLineSection((int)sectionKeyB, renderType: RenderType.Text);
                    var currentRevisionTextLines = new CodeLine[] { };
                    diffLines = InlineDiff.Compute(
                        diffRevisionTextLines,
                        currentRevisionTextLines,
                        diffRevisionHtmlLines,
                        activeRevisionHTMLLines);
                }
                
                var headingsOfSectionsWithDiff = diffRevision.HeadingsOfSectionsWithDiff.ContainsKey(activeRevision.Id) ? 
                    diffRevision.HeadingsOfSectionsWithDiff[activeRevision.Id] : new HashSet<int>();

                codeLines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: diffLines, comments: comments,
                    showDiffOnly: false, reviewDiffContextSize: diffContextSize, diffContextSeparator: diffContextSeperator,
                    headingsOfSectionsWithDiff: headingsOfSectionsWithDiff, language: activeRevision.Language);
            }
            else
            {
                activeRevisionHTMLLines = activeRevisionRenderableCodeFile.GetCodeLineSection(sectionKey);
                codeLines = PageModelHelpers.CreateLines(diagnostics: fileDiagnostics, lines: activeRevisionHTMLLines, comments: comments, hideCommentRows: true, language: activeRevision.Language);
            }
            return codeLines;
        }

        /// <summary>
        /// Ensure unique label for Revisions
        /// </summary>
        /// <param name="apiRevision"></param>
        /// <param name="addAPIRevisionType"></param>
        /// <param name="addCreatedBy"></param>
        /// <param name="addCreatedOn"></param>
        /// <param name="addPackageVersion"></param>
        /// <returns></returns>
        public static string ResolveRevisionLabel(APIRevisionListItemModel apiRevision, 
            bool addAPIRevisionType = true, bool addCreatedBy = true, bool addCreatedOn = true, bool addPackageVersion = true)
        {
            var label = String.Empty;
            
            if (apiRevision.APIRevisionType != APIRevisionType.Automatic && addCreatedBy)
                label = $"{apiRevision.CreatedBy}";

            if (addCreatedOn)
                label = $"{apiRevision.CreatedOn.ToString()} | {label}";

            if (addPackageVersion && apiRevision.Files.Any() && !String.IsNullOrEmpty(apiRevision.Files[0].PackageVersion) && (String.IsNullOrEmpty(apiRevision.Label) || !apiRevision.Label.Contains(apiRevision.Files[0].PackageVersion)))
                label = $"{apiRevision.Files[0].PackageVersion} | {label}";

            if (!String.IsNullOrWhiteSpace(apiRevision.Label))
                label = $"{apiRevision.Label} | {label}";

            if (apiRevision.APIRevisionType == APIRevisionType.PullRequest && apiRevision.PullRequestNo != null)
                label = $"PR {apiRevision.PullRequestNo} | {label}";

            if (addAPIRevisionType)
                label = $"{apiRevision.APIRevisionType.ToString()} | {label}";

            return label.Trim(' ', '|');
        }

        /// <summary>
        /// Upload API Revision
        /// </summary>
        /// <param name="apiRevisionsManager"></param>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <param name="upload"></param>
        /// <param name="label"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static async Task<APIRevisionListItemModel> UploadAPIRevisionAsync(IAPIRevisionsManager apiRevisionsManager, ClaimsPrincipal user,  string id, [FromForm] IFormFile upload, [FromForm] string label, [FromForm] string filePath) 
        {
            if (upload != null)
            {
                var openReadStream = upload.OpenReadStream();
                return await apiRevisionsManager.AddAPIRevisionAsync(user, id, APIRevisionType.Manual, upload.FileName, label, openReadStream, language: null);
            }
            else
            {
                return await apiRevisionsManager.AddAPIRevisionAsync(user, id, APIRevisionType.Manual, filePath, label, null);
            }
        }
        
        /// <summary>
        /// Decide if the content of the API is the header or content
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static bool IsAPIStubHeader(string content)
        {
            List<String> apiStubHEaders = new List<String> { ">model<", ">enum<", ">alias<", ">module<", ">package<", ">class<", ">interface<", ">def<" };
            return apiStubHEaders.Any(content.Contains);
        }
        /// <summary>
        /// Create DiffOnly Lines
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="reviewDiffContextSize"></param>
        /// <param name="diffContextSeparator"></param>
        /// <returns></returns>

        private static InlineDiffLine<CodeLine>[] CreateDiffOnlyLines(InlineDiffLine<CodeLine>[] lines, int reviewDiffContextSize, string diffContextSeparator)
        {
            var filteredLines = new List<InlineDiffLine<CodeLine>>();
            int lastAddedLine = -1;
            for (int i = 0; i < lines.Count(); i++)
            {
                if (lines[i].Kind != DiffLineKind.Unchanged)
                {
                    // Find starting index for pre context
                    int preContextIndx = Math.Max(lastAddedLine + 1, i - reviewDiffContextSize);
                    if (preContextIndx < i)
                    {
                        // Add sepearator to show skipping lines. for e.g. .....
                        if (filteredLines.Count > 0)
                        {
                            filteredLines.Add(new InlineDiffLine<CodeLine>(new CodeLine(html: diffContextSeparator, id: null, crossLangId: null, lineClass: null), DiffLineKind.Unchanged));
                        }

                        while (preContextIndx < i)
                        {
                            filteredLines.Add(lines[preContextIndx]);
                            preContextIndx++;
                        }
                    }
                    //Add changed line
                    filteredLines.Add(lines[i]);
                    lastAddedLine = i;

                    // Add post context
                    int contextStart = i + 1, contextEnd = i + reviewDiffContextSize;
                    while (contextStart <= contextEnd && contextStart < lines.Count() && lines[contextStart].Kind == DiffLineKind.Unchanged)
                    {
                        filteredLines.Add(lines[contextStart]);
                        lastAddedLine = contextStart;
                        contextStart++;
                    }
                }
            }
            return filteredLines.ToArray();
        }
        /// <summary>
        /// Create and Assign Approval Check Boxes
        /// </summary>
        /// <param name="Model"></param>
        /// <returns></returns>
        public static (string modalId, List<string> issueList, Dictionary<string, (string modalBody, string checkboxId, string checkboxName)> issueDict) GetModalInfo(ReviewPageModel Model)
        {
            var issueDict = new Dictionary<string, (string modalBody, string checkboxId, string checkboxName)>
            {
                ["openConvos"] = ("Active Conversations Present:", "overrideConvo", "overrideConvo"),
                ["fatalDiagnostics"] = ("Fatal Diagnostics Present:", "overrideDiag", "overrideDiag")
            };

            var issueList = new List<string>();
            string modalId = "";

            if ((Model.ReviewContent.ActiveConversationsInActiveAPIRevision > 0 || Model.ReviewContent.ActiveConversationsInSampleRevisions > 0) && Model.ReviewContent.HasFatalDiagnostics)
            {
                modalId = "convoFatalModel";
                issueList = new List<string> { "openConvos", "fatalDiagnostics" };
            }
            else if ((Model.ReviewContent.ActiveConversationsInActiveAPIRevision > 0 || Model.ReviewContent.ActiveConversationsInSampleRevisions > 0) && !Model.ReviewContent.HasFatalDiagnostics)
            {
                modalId = "openConversationModel";
                issueList = new List<string> { "openConvos" };
            }
            else
            {
                modalId = "fatalErrorModel";
                issueList = new List<string> { "fatalDiagnostics" };
            }

            return (modalId, issueList, issueDict);
        }
    }
}
