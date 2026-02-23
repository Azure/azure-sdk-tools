using ApiView;
using APIView.DIff;
using APIView.Model;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public class APIRevisionsManager : IAPIRevisionsManager
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ICosmosReviewRepository _reviewsRepository;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly ICosmosAPIRevisionsRepository _apiRevisionsRepository;
        private readonly IDiagnosticCommentService _diagnosticCommentService;
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IDevopsArtifactRepository _devopsArtifactRepository;
        private readonly IBlobOriginalsRepository _originalsRepository;
        private readonly INotificationManager _notificationManager;
        private readonly TelemetryClient _telemetryClient;
        private readonly HashSet<string> _upgradeDisabledLangs = new HashSet<string>();

        public APIRevisionsManager(
            IAuthorizationService authorizationService,
            ICosmosReviewRepository reviewsRepository,
            ICosmosAPIRevisionsRepository apiRevisionsRepository,
            IDiagnosticCommentService diagnosticCommentService,
            IHubContext<SignalRHub> signalRHubContext,
            IEnumerable<LanguageService> languageServices,
            IDevopsArtifactRepository devopsArtifactRepository,
            ICodeFileManager codeFileManager,
            IBlobCodeFileRepository codeFileRepository,
            IBlobOriginalsRepository originalsRepository,
            INotificationManager notificationManager,
            TelemetryClient telemetryClient,
            IConfiguration configuration)
        {
            _reviewsRepository = reviewsRepository;
            _apiRevisionsRepository = apiRevisionsRepository;
            _diagnosticCommentService = diagnosticCommentService;
            _authorizationService = authorizationService;
            _signalRHubContext = signalRHubContext;
            _codeFileManager = codeFileManager;
            _codeFileRepository = codeFileRepository;
            _languageServices = languageServices;
            _devopsArtifactRepository = devopsArtifactRepository;
            _originalsRepository = originalsRepository;
            _notificationManager = notificationManager;
            _telemetryClient = telemetryClient;
            var backgroundTaskDisabledLangs = configuration["ReviewUpdateDisabledLanguages"];
            if(!string.IsNullOrEmpty(backgroundTaskDisabledLangs))
            {
                _upgradeDisabledLangs.UnionWith(backgroundTaskDisabledLangs.Split(','));
            }
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pageParams"></param> Contains pagination info
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams)
        {
            return await _apiRevisionsRepository.GetAPIRevisionsAsync(user, pageParams, filterAndSortParams);
        }

        /// <summary>
        /// Retrieve Revisions for a particular Review from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="reviewId"></param> The Reviewid for which the revisions are to be retrieved
        /// <param name="packageVersion"></param> Optional package version param to return a matching revision for the package version 
        /// <param name="apiRevisionType"></param> optional API revision type filter
        /// <returns></returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId, string packageVersion = "", APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            var apiRevisions = await _apiRevisionsRepository.GetAPIRevisionsAsync(reviewId);

            if (apiRevisionType != APIRevisionType.All)
                apiRevisions = apiRevisions.Where(r => r.APIRevisionType == apiRevisionType);

            if (!string.IsNullOrEmpty(packageVersion))
            {                
                // Check for exact same package version
                // If exact version is not found in revision then search for same major and minor version and return the latest.
                var exactMatchRevisions = apiRevisions.Where(r => packageVersion.Equals(r.Files[0].PackageVersion));
                if (exactMatchRevisions.Any())
                {
                    return exactMatchRevisions.OrderByDescending(r => r.CreatedOn);
                }

                // Check for revisions with matching
                var versionGroups = packageVersion.Split('.');
                var majorMinor = $"{versionGroups[0]}.{versionGroups[1]}.";
                var majorMinorMatchRevisions = apiRevisions.Where(r => !string.IsNullOrEmpty(r.Files[0].PackageVersion) && r.Files[0].PackageVersion.StartsWith(majorMinor));
                if (majorMinorMatchRevisions.Any())
                {
                    return majorMinorMatchRevisions.OrderByDescending(r => r.CreatedOn);
                }                
                return majorMinorMatchRevisions;
            }
            return apiRevisions;
        }

        /// <summary>
        /// Retrieve the latest APRevison for a particular Review.
        /// Filter by APIRevisionType if specified and Review contains specified type
        /// If APIRevisionType is not specified, return the latest revision irrespective of the type
        /// Return default if no revisoin is found
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisions"></param> The list of revisions can be supplied if available to avoid another call to the database
        /// <param name="apiRevisionType"></param>
        /// <returns>APIRevisionListItemModel</returns>
        public async Task<APIRevisionListItemModel> GetLatestAPIRevisionsAsync(string reviewId = null, IEnumerable<APIRevisionListItemModel> apiRevisions = null, APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            if (reviewId == null && apiRevisions == null)
            { 
                throw new ArgumentException("Either reviewId or apiRevisions must be supplied");
            }

            if (apiRevisions == null)
            {
                apiRevisions = await _apiRevisionsRepository.GetAPIRevisionsAsync(reviewId);
            }

            if (apiRevisionType != APIRevisionType.All && apiRevisions.Any(r => r.APIRevisionType == apiRevisionType))
            {
                apiRevisions = apiRevisions.Where(r => r.APIRevisionType == apiRevisionType);
            }
            return apiRevisions.OrderByDescending(r => r.CreatedOn).FirstOrDefault();
        }

        /// <summary>
        /// Retrieve Revisions from the APIRevisions container in CosmosDb for a given crossLanguageId and language
        /// </summary>
        /// <param name="crossLanguageId"></param>
        /// <param name="language"></param>
        /// <param name="apiRevisionType"></param>
        /// <returns>APIRevisionListItemModel</returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetCrossLanguageAPIRevisionsAsync(string crossLanguageId, string language, APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            return await this._apiRevisionsRepository.GetCrossLanguageAPIRevisionsAsync(crossLanguageId, language, apiRevisionType);
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="apiRevisionId"></param> The RevisionId for which the revision is to be retrieved
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> GetAPIRevisionAsync(ClaimsPrincipal user, string apiRevisionId)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            var revisionModel = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId);
            return await UpgradeAPIRevisionIfRequired(revisionModel);
        }

        public async Task<APIRevisionListItemModel> GetAPIRevisionAsync(string apiRevisionId)
        {
            var revisionModel = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId);
            return await UpgradeAPIRevisionIfRequired(revisionModel);
        }

        /// <summary>
        /// GetNewAPIRevisionAsync
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="packageName"></param>
        /// <param name="language"></param>
        /// <param name="label"></param>
        /// <param name="prNumber"></param>
        /// <param name="createdBy"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="sourceBranch"></param>
        /// <returns></returns>
        public APIRevisionListItemModel GetNewAPIRevisionAsync(APIRevisionType apiRevisionType,
            string reviewId = null, string packageName = null, string language = null,
            string label = null, int? prNumber = null, string createdBy= ApiViewConstants.AzureSdkBotName, string sourceBranch = null)
        {
            var apiRevision = new APIRevisionListItemModel()
            {
                CreatedBy = createdBy,
                CreatedOn = DateTime.UtcNow,
                APIRevisionType = apiRevisionType,
                ChangeHistory =
                [
                    new APIRevisionChangeHistoryModel
                    {
                        ChangeAction = APIRevisionChangeAction.Created,
                        ChangedBy = createdBy,
                        ChangedOn = DateTime.UtcNow
                    }
                ],
            };

            if (!string.IsNullOrEmpty(reviewId))
            {
                apiRevision.ReviewId = reviewId;
            }

            if (!string.IsNullOrEmpty(packageName))
            {
                apiRevision.PackageName = packageName;
            }

            if (!string.IsNullOrEmpty(language))
            {
                apiRevision.Language = language;
            }

            if (!string.IsNullOrEmpty(label))
            {
                apiRevision.Label = label;
            }

            if (!string.IsNullOrEmpty(sourceBranch))
            {
                apiRevision.SourceBranch = sourceBranch;
            }

            if (prNumber != null)
            {
                apiRevision.PullRequestNo = prNumber;
            }

            return apiRevision;
        }

        /// <summary>
        /// Add new Approval or ApprovalReverted action to the ChangeHistory of a Revision
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="apiRevision"></param>
        /// <param name="notes"></param>
        /// <param name="approver"></param>
        /// <returns>true if review approval needs to be updated otherwise false</returns>
        public async Task<(bool updateReview, APIRevisionListItemModel apiRevision)> ToggleAPIRevisionApprovalAsync(ClaimsPrincipal user, string id, string apiRevisionId = null, APIRevisionListItemModel apiRevision = null, string notes = "", string approver = "")
        {
            if (apiRevisionId == null && apiRevision == null)
            {
                throw new ArgumentException(message: "apiRevisionId and apiRevision cannot both be null");
            }

            bool updateReview = false;
            if (apiRevision == null)
            {
                apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId: apiRevisionId);
            }
            ReviewListItemModel review = await _reviewsRepository.GetReviewAsync(apiRevision.ReviewId);

            await ManagerHelpers.AssertApprover<APIRevisionListItemModel>(user, apiRevision, _authorizationService);
            // Approver name also needs to be copied over when approval status is copied over.
            var userId = string.IsNullOrEmpty(approver) ? user.GetGitHubLogin() : approver;
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(apiRevision.ChangeHistory, APIRevisionChangeAction.Approved, userId, notes);
            apiRevision.ChangeHistory = changeUpdate.ChangeHistory;
            apiRevision.IsApproved = changeUpdate.ChangeStatus;
            if (ChangeHistoryHelpers.GetChangeActionStatus(apiRevision.ChangeHistory, APIRevisionChangeAction.Approved, userId))
            {
                apiRevision.Approvers.Add(userId);
            }
            else
            {
                apiRevision.Approvers.Remove(userId);
            }

            if (!review.IsApproved && apiRevision.IsApproved)
            {
                updateReview = true; // If review is not approved and revision is approved, update review
            }

            await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            // No need to send approval status to self when approval is copied over automatically
            if (userId == user.GetGitHubLogin())
            {
                await _signalRHubContext.Clients.Group(userId).SendAsync("ReceiveApprovalSelf", id, apiRevisionId, apiRevision.IsApproved);
            }
            
            await _signalRHubContext.Clients.All.SendAsync("ReceiveApproval", id, apiRevisionId, userId, apiRevision.IsApproved);
            return (updateReview, apiRevision);
        }

      
        private static void ApplyApprovalFrom(APIRevisionListItemModel targetRevision, APIRevisionListItemModel sourceRevision)
        {
            if (!sourceRevision.IsApproved)
            {
                throw new ArgumentException("Source revision must be approved to copy approval from", nameof(sourceRevision));
            }

            string approver = sourceRevision.Approvers.LastOrDefault();
            if (string.IsNullOrEmpty(approver))
            {
                throw new InvalidOperationException("Source revision has no approvers to copy from");
            }

            string notes = $"Approval copied from revision {sourceRevision.Id}";
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(targetRevision.ChangeHistory, APIRevisionChangeAction.Approved, approver, notes);
            targetRevision.ChangeHistory = changeUpdate.ChangeHistory;
            targetRevision.IsApproved = changeUpdate.ChangeStatus;
            targetRevision.Approvers.Add(approver);
        }

        /// <summary>
        /// Carry forward revision data from a source revision to a target revision when they have the same API surface.
        /// This copies properties that should be preserved 
        /// across revisions with identical API surfaces.
        /// </summary>
        /// <param name="targetRevision">The revision to copy data to</param>
        /// <param name="sourceRevision">The revision to copy data from</param>
        public async Task CarryForwardRevisionDataAsync(APIRevisionListItemModel targetRevision, APIRevisionListItemModel sourceRevision)
        {
            ArgumentNullException.ThrowIfNull(targetRevision);
            ArgumentNullException.ThrowIfNull(sourceRevision);

            bool dataChanged = false;

            // Copy approval if source is approved and target is not
            if (!targetRevision.IsApproved && sourceRevision.IsApproved)
            {
                ApplyApprovalFrom(targetRevision, sourceRevision);
                dataChanged = true;
            }

            if (!targetRevision.HasAutoGeneratedComments && sourceRevision.HasAutoGeneratedComments)
            {
                targetRevision.HasAutoGeneratedComments = true;
                dataChanged = true;
            }

            if (dataChanged)
            {
                await _apiRevisionsRepository.UpsertAPIRevisionAsync(targetRevision);
            }
        }

        /// <summary>
        /// Create APIRevision from File or FilePAth
        /// </summary>
        /// <param name="user"></param>
        /// <param name="review"></param>
        /// <param name="file"></param>
        /// <param name="filePath"></param>
        /// <param name="language"></param>
        /// <param name="label"></param>
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> CreateAPIRevisionAsync(ClaimsPrincipal user, ReviewListItemModel review, IFormFile file, string filePath, string language, string label)
        {
            APIRevisionListItemModel apiRevision = null;

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    apiRevision = await AddAPIRevisionAsync(user: user, review: review, apiRevisionType: APIRevisionType.Manual,
                        name: file.FileName, label: label, fileStream: openReadStream, language: language);
                }
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                apiRevision = await AddAPIRevisionAsync(user: user, review: review, apiRevisionType: APIRevisionType.Manual,
                           name: filePath, label: label, fileStream: null, language: language);
            }
            return apiRevision;
        }


        public async Task<string> GetOutlineAPIRevisionsAsync(string activeApiRevisionId)
        {
            APIRevisionListItemModel activeApiRevision = await GetAPIRevisionAsync(activeApiRevisionId);
            RenderedCodeFile activeCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision, false);
            return activeCodeFile.CodeFile.GetApiOutlineText();
        }

        public async Task<string> GetApiRevisionText(APIRevisionListItemModel activeApiRevision)
        {
            RenderedCodeFile activeRevisionCodeFile = await _codeFileRepository.GetCodeFileAsync(activeApiRevision);
            return activeRevisionCodeFile.CodeFile.GetApiText();
        }

        /// <summary>
        /// Add API Revision to Review
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="name"></param>
        /// <param name="label"></param>
        /// <param name="fileStream"></param>
        /// <param name="language"></param>
        /// <param name="awaitComputeDiff"></param>
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> AddAPIRevisionAsync(
            ClaimsPrincipal user,
            string reviewId,
            APIRevisionType apiRevisionType,
            string name,
            string label,
            Stream fileStream,
            string language = "",
            bool awaitComputeDiff = false)
        {
            var review = await _reviewsRepository.GetReviewAsync(reviewId);
            return await AddAPIRevisionAsync(user, review, apiRevisionType, name, label, fileStream, language, awaitComputeDiff);
        }

        /// <summary>
        /// For reviews with collapsible sections (Swagger). Precomputs the line numbers of the headings with diff
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevision"></param>
        /// <param name="apiRevisions"></param>
        /// <returns></returns>
        public async Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, APIRevisionListItemModel apiRevision, IEnumerable<APIRevisionListItemModel> apiRevisions = null)
        {
            if (apiRevisions == null)
            {
                apiRevisions = await _apiRevisionsRepository.GetAPIRevisionsAsync(reviewId);
            } 
            var RevisionACodeFile = await _codeFileRepository.GetCodeFileAsync(apiRevision, false);
            var RevisionAHtmlLines = RevisionACodeFile.Render(false);
            var RevisionATextLines = RevisionACodeFile.RenderText(false);

            var latestFewRevisions = apiRevisions.Count() > 10? apiRevisions.OrderBy(r => r.CreatedOn).Reverse().Take(10) : apiRevisions;
            foreach (var rev in latestFewRevisions)
            {
                if (rev.Id != apiRevision.Id)
                {
                    var lineNumbersForHeadingOfSectionWithDiff = new HashSet<int>();
                    var RevisionBCodeFile = await _codeFileRepository.GetCodeFileAsync(rev, false);
                    var RevisionBHtmlLines = RevisionBCodeFile.RenderReadOnly(false);
                    var RevisionBTextLines = RevisionBCodeFile.RenderText(false);


                    // Compute diff before: apiRevision -> after: existing APIRevision
                    var diffLines = InlineDiff.Compute(before: RevisionATextLines, after: RevisionBTextLines, beforeResults: RevisionAHtmlLines, afterResults: RevisionBHtmlLines);

                    Parallel.ForEach(diffLines, diffLine =>
                    {
                        if (diffLine.Kind == DiffLineKind.Unchanged && diffLine.Line.SectionKey != null && diffLine.OtherLine.SectionKey != null)
                        {
                            var RevisionARootNode = RevisionACodeFile.GetCodeLineSectionRoot((int)diffLine.Line.SectionKey);
                            var RevisionBRootNode = RevisionBCodeFile.GetCodeLineSectionRoot((int)diffLine.OtherLine.SectionKey);

                            if (RevisionARootNode != null && RevisionBRootNode != null)
                            {
                                var diffSectionRoot = ComputeSectionDiff(before: RevisionARootNode, after: RevisionBRootNode, beforeFile: RevisionACodeFile, afterFile: RevisionBCodeFile);
                                if (RevisionACodeFile.ChildNodeHasDiff(diffSectionRoot))
                                    lineNumbersForHeadingOfSectionWithDiff.Add((int)diffLine.Line.LineNumber);
                            }
                        }
                    });

                    if (apiRevision.HeadingsOfSectionsWithDiff.ContainsKey(rev.Id))
                    {
                        apiRevision.HeadingsOfSectionsWithDiff.Remove(rev.Id);
                    }
                    if (lineNumbersForHeadingOfSectionWithDiff.Any())
                    {
                        apiRevision.HeadingsOfSectionsWithDiff.Add(rev.Id, lineNumbersForHeadingOfSectionWithDiff);
                    }
                    await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);

                    // Compute diff before: existing APIRevision -> after: apiRevision
                    diffLines = InlineDiff.Compute(before: RevisionBTextLines, after: RevisionATextLines, beforeResults: RevisionBHtmlLines, afterResults: RevisionAHtmlLines);

                    Parallel.ForEach(diffLines, diffLine =>
                    {
                        if (diffLine.Kind == DiffLineKind.Unchanged && diffLine.Line.SectionKey != null && diffLine.OtherLine.SectionKey != null)
                        {
                            var RevisionBRootNode = RevisionBCodeFile.GetCodeLineSectionRoot((int)diffLine.Line.SectionKey);
                            var RevisionARootNode = RevisionACodeFile.GetCodeLineSectionRoot((int)diffLine.OtherLine.SectionKey);

                            if (RevisionARootNode != null && RevisionBRootNode != null)
                            {
                                var diffSectionRoot = ComputeSectionDiff(before: RevisionBRootNode, after: RevisionARootNode, beforeFile: RevisionBCodeFile, afterFile: RevisionACodeFile);
                                if (RevisionACodeFile.ChildNodeHasDiff(diffSectionRoot))
                                    lineNumbersForHeadingOfSectionWithDiff.Add((int)diffLine.Line.LineNumber);
                            }
                        }
                    });

                    if (rev.HeadingsOfSectionsWithDiff.ContainsKey(apiRevision.Id))
                    {
                        rev.HeadingsOfSectionsWithDiff.Remove(apiRevision.Id);
                    }
                    if (lineNumbersForHeadingOfSectionWithDiff.Any())
                    {
                        rev.HeadingsOfSectionsWithDiff.Add(apiRevision.Id, lineNumbersForHeadingOfSectionWithDiff);
                    }
                    await _apiRevisionsRepository.UpsertAPIRevisionAsync(rev);
                }
                
            }
        }

        /// <summary>
        /// Computed the diff for hidden (collapsible) API sections
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        /// <param name="beforeFile"></param>
        /// <param name="afterFile"></param>
        /// <returns></returns>
        public TreeNode<InlineDiffLine<CodeLine>> ComputeSectionDiff(TreeNode<CodeLine> before, TreeNode<CodeLine> after, RenderedCodeFile beforeFile, RenderedCodeFile afterFile)
        {
            var rootDiff = new InlineDiffLine<CodeLine>(before.Data, after.Data, DiffLineKind.Unchanged);
            var resultRoot = new TreeNode<InlineDiffLine<CodeLine>>(rootDiff);

            var queue = new Queue<(TreeNode<CodeLine> before, TreeNode<CodeLine> after, TreeNode<InlineDiffLine<CodeLine>> current)>();

            queue.Enqueue((before, after, resultRoot));

            while (queue.Count > 0)
            {
                var nodesInProcess = queue.Dequeue();
                var (beforeHTMLLines, beforeTextLines) = GetCodeLinesForDiff(nodesInProcess.before, nodesInProcess.current, beforeFile);
                var (afterHTMLLines, afterTextLines) = GetCodeLinesForDiff(nodesInProcess.after, nodesInProcess.current, afterFile);

                var diffResult = InlineDiff.Compute(beforeTextLines, afterTextLines, beforeHTMLLines, afterHTMLLines);

                if (diffResult.Count() == 2 &&
                    diffResult[0]!.Line.NodeRef != null && diffResult[1]!.Line.NodeRef != null &&
                    diffResult[0]!.Line.NodeRef.IsLeaf && diffResult[1]!.Line.NodeRef.IsLeaf) // Detached Leaf Parents which are Eventually Discarded
                {
                    var inlineDiffLine = new InlineDiffLine<CodeLine>(diffResult[1].Line, diffResult[0].Line, DiffLineKind.Unchanged);
                    diffResult = new InlineDiffLine<CodeLine>[] { inlineDiffLine };
                }

                foreach (var diff in diffResult)
                {
                    var addedChild = nodesInProcess.current.AddChild(diff);

                    switch (diff.Kind)
                    {
                        case DiffLineKind.Removed:
                            queue.Enqueue((diff.Line.NodeRef, null, addedChild));
                            break;
                        case DiffLineKind.Added:
                            queue.Enqueue((null, diff.Line.NodeRef, addedChild));
                            break;
                        case DiffLineKind.Unchanged:
                            queue.Enqueue((diff.OtherLine.NodeRef, diff.Line.NodeRef, addedChild));
                            break;
                    }
                }
            }
            return resultRoot;
        }

        /// <summary>
        /// Add APIRevision
        /// </summary>
        /// <param name="user"></param>
        /// <param name="review"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="name"></param>
        /// <param name="label"></param>
        /// <param name="fileStream"></param>
        /// <param name="language"></param>
        /// <param name="awaitComputeDiff"></param>
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> AddAPIRevisionAsync(
            ClaimsPrincipal user,
            ReviewListItemModel review,
            APIRevisionType apiRevisionType,
            string name,
            string label,
            Stream fileStream,
            string language,
            bool awaitComputeDiff = false)
        {
            var apiRevision = GetNewAPIRevisionAsync(
                reviewId: review.Id,
                apiRevisionType: apiRevisionType,
                packageName: review.PackageName,
                language: review.Language,
                createdBy: user.GetGitHubLogin(),
                label: label);

            APICodeFileModel codeFileModel = await _codeFileManager.CreateCodeFileAsync(
                apiRevision.Id,
                name,
                true,
                fileStream,
                language);

            apiRevision.Files.Add(codeFileModel);

            var languageService = language != null ? _languageServices.FirstOrDefault(l => l.Name == language) : _languageServices.FirstOrDefault(s => s.IsSupportedFile(name));
            bool isPipelineGenerated = languageService != null && languageService.IsReviewGenByPipeline;
            
            // Run pipeline to generate the review if sandbox is enabled
            if (isPipelineGenerated)
            {
                // Run offline review gen for review and reviewCodeFileModel
                await GenerateAPIRevisionInExternalResource(review, apiRevision.Id, codeFileModel.FileId, name, language);
            }
            else
            {
                CodeFile codeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(apiRevision.Id, codeFileModel.FileId);
                if (codeFile?.Diagnostics != null && codeFile.Diagnostics.Length > 0)
                {
                    DiagnosticSyncResult diagnosticResult = await _diagnosticCommentService.SyncDiagnosticCommentsAsync(
                        review.Id,
                        apiRevision.Id,
                        null, // No existing hash for new revisions
                        codeFile.Diagnostics,
                        []);
                    
                    apiRevision.DiagnosticsHash = diagnosticResult.DiagnosticsHash;
                }
            }

            // auto subscribe revision creation user
            await _notificationManager.SubscribeAsync(review, user);
            await _reviewsRepository.UpsertReviewAsync(review);
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            await _notificationManager.NotifySubscribersOnNewRevisionAsync(review, apiRevision, user);

            if (!String.IsNullOrEmpty(review.Language) && review.Language == ApiViewConstants.SwaggerLanguage)
            {
                if (awaitComputeDiff)
                {
                    await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.Id, apiRevision);
                }
                else
                {
                    _ = Task.Run(async () => await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.Id, apiRevision));
                }
            }
            return apiRevision;
            //await GenerateAIReview(review, revision);
        }

        /// <summary>
        /// Run Pipeline to generate API Revision
        /// </summary>
        /// <param name="reviewGenParams"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public async Task RunAPIRevisionGenerationPipeline(List<APIRevisionGenerationPipelineParamModel> reviewGenParams, string language)
        {
            var jsonSerializerOptions = new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            var reviewParamString = JsonSerializer.Serialize(reviewGenParams, jsonSerializerOptions);
            reviewParamString = reviewParamString.Replace("\"", "'");
            await _devopsArtifactRepository.RunPipeline($"tools - generate-{language}-apireview",
                reviewParamString,
                _originalsRepository.GetContainerUrl());
        }

        /// <summary>
        /// Delete APIRevisions
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        public async Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string apiRevisionId)
        {
            var apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId: apiRevisionId);
            ManagerHelpers.AssertAPIRevisionDeletion(apiRevision);
            await ManagerHelpers.AssertAPIRevisionOwner(user, apiRevision, _authorizationService);
            await SoftDeleteAPIRevisionAsync(user, apiRevision);
        }

        /// <summary>
        /// Restore APIRevisions
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionId"></param>
        /// <returns></returns>
        public async Task RestoreAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string apiRevisionId)
        {
            var apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId: apiRevisionId);
            ManagerHelpers.AssertAPIRevisionDeletion(apiRevision);
            await ManagerHelpers.AssertAPIRevisionOwner(user, apiRevision, _authorizationService);
            if (apiRevision.IsDeleted)
            {
                var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(
                     changeHistory: apiRevision.ChangeHistory, action: APIRevisionChangeAction.UnDeleted, user: user.GetGitHubLogin(), notes: "");

                apiRevision.ChangeHistory = changeUpdate.ChangeHistory;
                apiRevision.IsDeleted = changeUpdate.ChangeStatus;

                await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            }
        }

        /// <summary>
        /// Delete APIRevisions
        /// </summary>
        /// <param name="user"></param>
        /// <param name="apiRevision"></param>
        /// <returns></returns>
        public async Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, APIRevisionListItemModel apiRevision)
        {
            ManagerHelpers.AssertAPIRevisionDeletion(apiRevision);
            await ManagerHelpers.AssertAPIRevisionOwner(user, apiRevision, _authorizationService);
            await SoftDeleteAPIRevisionAsync(userName: user.GetGitHubLogin(), apiRevision: apiRevision);
        }

        /// <summary>
        /// Delete APIRevisions
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="apiRevision"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public async Task SoftDeleteAPIRevisionAsync(APIRevisionListItemModel apiRevision, string userName = ApiViewConstants.AzureSdkBotName, string notes = "")
        {
            if (!apiRevision.IsDeleted)
            {
                _telemetryClient.TrackTrace($"Soft-deleting API revision. RevisionId={apiRevision.Id}, ReviewId={apiRevision.ReviewId}, User={userName}, Notes={notes}");

                var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(
                     changeHistory: apiRevision.ChangeHistory, action: APIRevisionChangeAction.Deleted, user: userName, notes: notes);

                apiRevision.ChangeHistory = changeUpdate.ChangeHistory;
                apiRevision.IsDeleted = changeUpdate.ChangeStatus;

                await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <param name="revisionId"></param>
        /// <param name="label"></param>
        /// <returns></returns>
        public async Task UpdateAPIRevisionLabelAsync(ClaimsPrincipal user, string revisionId, string label)
        {
            var revision = await GetAPIRevisionAsync(user, revisionId);
            await ManagerHelpers.AssertAPIRevisionOwner(user, revision, _authorizationService);
            revision.Label = label;
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
        }

        /// <summary>
        /// UpdateAPIRevisionCodeFileAsync
        /// </summary>
        /// <param name="repoName"></param>
        /// <param name="buildId"></param>
        /// <param name="artifact"></param>
        /// <param name="project"></param>
        /// <returns></returns>
        public async Task UpdateAPIRevisionCodeFileAsync(string repoName, string buildId, string artifact, string project)
        {
            var stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifact, filePath: null, project: project, format: "zip");
            var archive = new ZipArchive(stream);
            foreach (var entry in archive.Entries)
            {
                var reviewFilePath = entry.FullName;
                var reviewDetails = reviewFilePath.Split("/");

                if (reviewDetails.Length < 4 || !reviewFilePath.EndsWith(".json"))
                    continue;

                var reviewId = reviewDetails[1];
                var apiRevisionId = reviewDetails[2];
                var review = await _reviewsRepository.GetReviewAsync(reviewId);

                var codeFile = await CodeFile.DeserializeAsync(entry.Open());

                // Update code file with one downloaded from pipeline
                if (review != null)
                {
                    var apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId: apiRevisionId);
                    if (apiRevision != null)
                    {
                        if (codeFile.CrossLanguageMetadata == null)
                        {
                            CodeFile existingCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(apiRevisionId, apiRevision.Files.Single().FileId);
                            if (existingCodeFile?.CrossLanguageMetadata != null)
                            {
                                codeFile.CrossLanguageMetadata = existingCodeFile.CrossLanguageMetadata;
                            }
                        }

                        await _codeFileRepository.UpsertCodeFileAsync(apiRevisionId, apiRevision.Files.Single().FileId, codeFile);
                        var file = apiRevision.Files.FirstOrDefault();
                        file.VersionString = codeFile.VersionString;
                        file.PackageName = codeFile.PackageName;
                        file.PackageVersion = codeFile.PackageVersion;
                        file.ParserStyle = codeFile.ReviewLines.Count > 0 ? ParserStyle.Tree : ParserStyle.Flat;
                        await _reviewsRepository.UpsertReviewAsync(review);
                        await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);

                        if (!String.IsNullOrEmpty(review.Language) && review.Language == ApiViewConstants.SwaggerLanguage)
                        {
                            // Trigger diff calculation using updated code file from sandboxing pipeline
                            await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.Id, apiRevision);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if APIRevision is the Same
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="renderedCodeFile"></param>
        /// <param name="considerPackageVersion"></param>
        /// <returns></returns>
        public async Task<bool> AreAPIRevisionsTheSame(APIRevisionListItemModel revision, RenderedCodeFile renderedCodeFile, bool considerPackageVersion = false)
        {
            //This will compare and check if new code file content is same as revision in parameter
            var lastRevisionFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var result = _codeFileManager.AreAPICodeFilesTheSame(codeFileA: lastRevisionFile, codeFileB: renderedCodeFile);
            if (considerPackageVersion)
            {
                return result && lastRevisionFile.CodeFile.PackageVersion == renderedCodeFile.CodeFile.PackageVersion;
            }
            return result;
        }

        /// <summary>
        /// Update APIRevision
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="languageService"></param>
        /// <param name="verifyUpgradabilityOnly"> </param>
        /// <returns></returns>
        public async Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision, LanguageService languageService, bool verifyUpgradabilityOnly)
        {
            foreach (var file in revision.Files)
            {
                if (!file.HasOriginal || !languageService.CanUpdate(file.VersionString))
                {
                    continue;
                }

                try
                {
                    var fileOriginal = await _originalsRepository.GetOriginalAsync(file.FileId);
                    if (string.IsNullOrEmpty(file.FileName))
                    {
                        _telemetryClient.TrackTrace($"Revision does not have original file name to update API revision. Revision Id: {revision.Id}");
                        continue;
                    }

                    CodeFile existingCodeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revision.Id, file.FileId);
                    CrossLanguageMetadata crossLanguageMetadata = existingCodeFile?.CrossLanguageMetadata;
                    string crossLanguageMetadataJson = null;
                    if (crossLanguageMetadata != null)
                    {
                        crossLanguageMetadataJson = JsonSerializer.Serialize(crossLanguageMetadata);
                    }

                    CodeFile codeFile = await languageService.GetCodeFileAsync(file.FileName, fileOriginal, false, crossLanguageMetadataJson);
                    if (!verifyUpgradabilityOnly)
                    {
                        await _codeFileRepository.UpsertCodeFileAsync(revision.Id, file.FileId, codeFile);
                        // update only version string
                        file.VersionString = codeFile.VersionString;
                        if (codeFile.ReviewLines.Count > 0)
                        {
                            file.ParserStyle = ParserStyle.Tree;
                        }
                        await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
                        _telemetryClient.TrackTrace($"Successfully Updated {revision.Language} revision with id {revision.Id}");
                    }
                    else
                    {
                        _telemetryClient.TrackTrace($"Revision with id {revision.Id} for package {codeFile.PackageName} cannot be upgraded using new parser version.");
                    }
                }
                catch (Exception ex)
                {
                    if (!verifyUpgradabilityOnly)
                        _telemetryClient.TrackTrace($"Failed to update {revision.Language} revision with id {revision.Id}");
                    else
                        _telemetryClient.TrackTrace($"Revision with id {revision.Id} for package {file.PackageName} cannot be upgraded using new parser version.");
                    _telemetryClient.TrackException(ex);
                }
            }
        }

        public async Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision)
        {
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
        }

        /// <summary>
        /// SoftDelete APIRevision if its not been updated after many months
        /// Preserves the last approved stable release and last preview release for each review
        /// </summary>
        /// <param name="archiveAfterMonths"></param>
        /// <returns></returns>
        public async Task AutoArchiveAPIRevisions(int archiveAfterMonths)
        {
            var lastUpdatedDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(archiveAfterMonths * 30));
            var manualRevisions = await _apiRevisionsRepository.GetAPIRevisionsAsync(lastUpdatedOn: lastUpdatedDate, apiRevisionType:  APIRevisionType.Manual);

            _telemetryClient.TrackTrace($"AutoArchive: Found {manualRevisions.Count()} manual revisions not updated since {lastUpdatedDate}");

            // Group revisions by ReviewId to identify which revisions to preserve
            var revisionsByReview = manualRevisions.GroupBy(r => r.ReviewId).ToList();
            var revisionsToPreserve = new HashSet<string>();

            // Fetch all revisions for affected reviews concurrently to avoid N+1 sequential calls
            var reviewIds = revisionsByReview.Select(g => g.Key).ToList();
            var revisionTasks = reviewIds
                .Select(id => _apiRevisionsRepository.GetAPIRevisionsAsync(id))
                .ToList();
            var revisionResults = await Task.WhenAll(revisionTasks);
            
            var allRevisionsDict = new Dictionary<string, IEnumerable<APIRevisionListItemModel>>();
            for (int i = 0; i < reviewIds.Count; i++)
            {
                allRevisionsDict[reviewIds[i]] = revisionResults[i];
            }

            foreach (var reviewGroup in revisionsByReview)
            {
                var allRevisionsForReview = allRevisionsDict[reviewGroup.Key];
                
                // Find the last approved stable release by creation date to avoid preserving edited old versions
                var lastApprovedStable = allRevisionsForReview
                    .Where(r => r.IsApproved && !IsPrerelease(r))
                    .OrderByDescending(r => r.CreatedOn)
                    .FirstOrDefault();
                
                if (lastApprovedStable != null)
                {
                    revisionsToPreserve.Add(lastApprovedStable.Id);
                }

                // Find the last preview release (approved or not) by creation date
                var lastPreview = allRevisionsForReview
                    .Where(r => IsPrerelease(r))
                    .OrderByDescending(r => r.CreatedOn)
                    .FirstOrDefault();
                
                if (lastPreview != null)
                {
                    revisionsToPreserve.Add(lastPreview.Id);
                }
            }

            _telemetryClient.TrackTrace($"AutoArchive: Preserving {revisionsToPreserve.Count} revisions (last stable/preview per review)");

            // Archive inactive revisions, excluding preserved ones
            int preservedCount = 0;
            int archivedCount = 0;
            
            foreach (var apiRevision in manualRevisions)
            {
                if (revisionsToPreserve.Contains(apiRevision.Id))
                {
                    preservedCount++;
                    continue;
                }

                var requestTelemetry = new RequestTelemetry { Name = "Archiving Revision " + apiRevision.Id };
                var operation = _telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    await SoftDeleteAPIRevisionAsync(apiRevision: apiRevision, notes: "Auto archived");
                    archivedCount++;
                    await Task.Delay(500);
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e, new Dictionary<string, string>
                    {
                        { "RevisionId", apiRevision.Id },
                        { "ReviewId", apiRevision.ReviewId },
                        { "ErrorType", "AutoArchiveFailure" }
                    });
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }
            
            _telemetryClient.TrackTrace($"AutoArchive: Completed. Archived={archivedCount}, Preserved={preservedCount}, TotalProcessed={manualRevisions.Count()}");

            // Log summary telemetry once per run instead of per revision
            _telemetryClient.TrackEvent("AutoArchiveAPIRevisions", new Dictionary<string, string>
            {
                { "PreservedCount", preservedCount.ToString() },
                { "ArchivedCount", archivedCount.ToString() },
                { "TotalProcessed", manualRevisions.Count().ToString() }
            });
        }

        /// <summary>
        /// Permanently deletes (hard deletes) API revisions that have been soft-deleted for a specified period.
        /// Only removes Manual and PullRequest revision types to preserve Automatic revisions for history.
        /// Deletes both Cosmos DB entries and associated blob storage (code files and originals).
        /// </summary>
        /// <param name="purgeAfterMonths">Number of months a revision must be soft-deleted before being purged</param>
        public async Task AutoPurgeAPIRevisions(int purgeAfterMonths)
        {
            const int DelayBetweenDeletionsMs = 500; // Rate limiting to avoid overwhelming services
            
            // AddMonths handles month-end edge cases correctly (e.g., Jan 31 minus 1 month = Dec 31)
            // This ensures accurate grace period calculation regardless of month lengths
            var deletedBeforeDate = DateTime.UtcNow.AddMonths(-purgeAfterMonths);

            _telemetryClient.TrackTrace($"AutoPurge: Starting. Looking for revisions soft-deleted before {deletedBeforeDate} (purgeAfterMonths={purgeAfterMonths})");
            
            // Query for soft-deleted Manual revisions
            var manualRevisions = await _apiRevisionsRepository.GetSoftDeletedAPIRevisionsAsync(
                deletedBefore: deletedBeforeDate, 
                apiRevisionType: APIRevisionType.Manual);
            
            // Query for soft-deleted PullRequest revisions
            var pullRequestRevisions = await _apiRevisionsRepository.GetSoftDeletedAPIRevisionsAsync(
                deletedBefore: deletedBeforeDate, 
                apiRevisionType: APIRevisionType.PullRequest);
            
            // Combine both types
            var revisionsToDelete = manualRevisions.Concat(pullRequestRevisions).ToList();

            _telemetryClient.TrackTrace($"AutoPurge: Found {revisionsToDelete.Count} revisions to purge (Manual={manualRevisions.Count()}, PullRequest={pullRequestRevisions.Count()})");
            
            int successCount = 0;
            int errorCount = 0;
            
            foreach (var apiRevision in revisionsToDelete)
            {
                var requestTelemetry = new RequestTelemetry { Name = "Purging Revision " + apiRevision.Id };
                var operation = _telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    _telemetryClient.TrackTrace($"AutoPurge: Purging revision. RevisionId={apiRevision.Id}, ReviewId={apiRevision.ReviewId}, FileCount={apiRevision.Files?.Count ?? 0}");

                    // Delete associated blobs (code files and originals)
                    foreach (var file in apiRevision.Files ?? new List<APICodeFileModel>())
                    {
                        try
                        {
                            // Delete code file blob
                            await _codeFileRepository.DeleteCodeFileAsync(apiRevision.Id, file.FileId);
                        }
                        catch (Exception ex)
                        {
                            // Log but continue - blob may not exist
                            _telemetryClient.TrackException(ex, new Dictionary<string, string>
                            {
                                { "RevisionId", apiRevision.Id },
                                { "FileId", file.FileId },
                                { "ErrorType", "CodeFileDeletion" }
                            });
                        }
                        
                        // Delete original file blob if it exists
                        if (file.HasOriginal)
                        {
                            try
                            {
                                await _originalsRepository.DeleteOriginalAsync(file.FileId);
                            }
                            catch (Exception ex)
                            {
                                // Log but continue - blob may not exist
                                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                                {
                                    { "RevisionId", apiRevision.Id },
                                    { "FileId", file.FileId },
                                    { "ErrorType", "OriginalFileDeletion" }
                                });
                            }
                        }
                    }
                    
                    // Delete Cosmos DB entry
                    await _apiRevisionsRepository.DeleteAPIRevisionAsync(apiRevision.Id, apiRevision.ReviewId);
                    
                    successCount++;
                    _telemetryClient.TrackTrace($"AutoPurge: Successfully purged revision. RevisionId={apiRevision.Id}, ReviewId={apiRevision.ReviewId}");
                    
                    // Small delay to avoid overwhelming services
                    await Task.Delay(DelayBetweenDeletionsMs);
                }
                catch (Exception e)
                {
                    errorCount++;
                    _telemetryClient.TrackException(e, new Dictionary<string, string>
                    {
                        { "RevisionId", apiRevision.Id },
                        { "ReviewId", apiRevision.ReviewId },
                        { "ErrorType", "PurgeFailure" }
                    });
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }
            
            _telemetryClient.TrackTrace($"AutoPurge: Completed. Success={successCount}, Errors={errorCount}, TotalProcessed={revisionsToDelete.Count}");

            // Log summary telemetry
            _telemetryClient.TrackEvent("AutoPurgeAPIRevisions", new Dictionary<string, string>
            {
                { "SuccessCount", successCount.ToString() },
                { "ErrorCount", errorCount.ToString() },
                { "TotalProcessed", revisionsToDelete.Count.ToString() },
                { "PurgeAfterMonths", purgeAfterMonths.ToString() }
            });
        }

        /// <summary>
        /// Determines if a revision is a prerelease based on its package version
        /// </summary>
        /// <param name="revision">The API revision to check</param>
        /// <returns>True if the revision is a prerelease, false otherwise</returns>
        private bool IsPrerelease(APIRevisionListItemModel revision)
        {
            var packageVersion = GetPackageVersion(revision);
            if (string.IsNullOrEmpty(packageVersion))
            {
                return false;
            }

            try
            {
                var semVer = new AzureEngSemanticVersion(packageVersion, revision.Language);
                return semVer.IsSemVerFormat && semVer.IsPrerelease;
            }
            catch
            {
                // If version parsing fails, treat as non-prerelease to be conservative
                return false;
            }
        }

        /// <summary>
        /// Safely extracts package version from a revision's files
        /// </summary>
        /// <param name="revision">The API revision</param>
        /// <returns>Package version string or null if not available</returns>
        private string GetPackageVersion(APIRevisionListItemModel revision)
        {
            if (revision.Files == null || !revision.Files.Any())
            {
                return null;
            }

            // Assume all files in a revision have the same package version
            // (revisions are typically for a single package version)
            return revision.Files.First().PackageVersion;
        }

        /// <summary>
        /// CreateAPIRevisionAsync
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="label"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <param name="originalName"></param>
        /// <param name="prNumber"></param>
        /// <param name="sourceBranch"></param>
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> CreateAPIRevisionAsync(string userName, string reviewId, APIRevisionType apiRevisionType, string label,
            MemoryStream memoryStream, CodeFile codeFile, string originalName = null, int? prNumber = null, string sourceBranch = null)
        {

            var apiRevision = GetNewAPIRevisionAsync(
                reviewId: reviewId,
                apiRevisionType: apiRevisionType,
                packageName: codeFile.PackageName,
                language: codeFile.Language,
                createdBy: userName,
                prNumber: prNumber,
                label: label);

            var apiRevisionCodeFile = await _codeFileManager.CreateReviewCodeFileModel(apiRevisionId: apiRevision.Id, memoryStream: memoryStream, codeFile: codeFile);
            apiRevision.Files.Add(apiRevisionCodeFile);
            if (!string.IsNullOrEmpty(originalName))
            {
                apiRevisionCodeFile.FileName = originalName;
            }

            DiagnosticSyncResult diagnosticResult = await _diagnosticCommentService.SyncDiagnosticCommentsAsync(
                reviewId,
                apiRevision.Id,
                null, 
                codeFile.Diagnostics,
                []);
            
            apiRevision.DiagnosticsHash = diagnosticResult.DiagnosticsHash;

            await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            return apiRevision;
        }

        /// <summary>
        /// Assign reviewers to a review
        /// </summary>
        /// <param name="User"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="reviewers"></param>
        /// <returns></returns>
        public async Task AssignReviewersToAPIRevisionAsync(ClaimsPrincipal User, string apiRevisionId, HashSet<string> reviewers)
        {
            APIRevisionListItemModel apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId);
            foreach (var reviewer in reviewers)
            {
                if (!apiRevision.AssignedReviewers.Where(x => x.AssingedTo == reviewer).Any())
                {
                    var reviewAssignment = new ReviewAssignmentModel()
                    {
                        AssingedTo = reviewer,
                        AssignedBy = User.GetGitHubLogin(),
                        AssingedOn = DateTime.Now,
                    };
                    apiRevision.AssignedReviewers.Add(reviewAssignment);
                }
            }
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
        }

        public async Task<APIRevisionListItemModel> UpdateAPIRevisionReviewersAsync(ClaimsPrincipal User, string apiRevisionId, HashSet<string> reviewers)
        {
            APIRevisionListItemModel apiRevision = await _apiRevisionsRepository.GetAPIRevisionAsync(apiRevisionId);
            foreach (var reviewer in reviewers)
            {
                if (!apiRevision.AssignedReviewers.Where(x => x.AssingedTo == reviewer).Any())
                {
                    var reviewAssignment = new ReviewAssignmentModel()
                    {
                        AssingedTo = reviewer,
                        AssignedBy = User.GetGitHubLogin(),
                        AssingedOn = DateTime.Now,
                    };
                    apiRevision.AssignedReviewers.Add(reviewAssignment);
                }
            }
            foreach (var assignment in apiRevision.AssignedReviewers.FindAll(x => !reviewers.Contains(x.AssingedTo)))
            {
                apiRevision.AssignedReviewers.Remove(assignment);
            }
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            return apiRevision;
        }

        /// <summary>
        /// Get Reviews that have been assigned for review to a user
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAssignedToUser(string userName)
        {
            return await _apiRevisionsRepository.GetAPIRevisionsAssignedToUser(userName);
        }

        public async Task<APIRevisionListItemModel> UpdateRevisionMetadataAsync(APIRevisionListItemModel revision, string packageVersion, string label, bool setReleaseTag = false)
        {
            // Do not update package version metadata once a revision is marked as released
            // This is to avoid updating metadata when a request is processed with a new version (auto incremented version change) right after a version is released
            // without any API changes.
            if (revision.IsReleased)
                return revision;

            if (packageVersion != null && !packageVersion.Equals(revision.Files[0].PackageVersion))
            {
                revision.Files[0].PackageVersion = packageVersion;
                revision.Label = label;
            }

            if (setReleaseTag)
            {
                revision.IsReleased = true;
                revision.ReleasedOn = DateTime.UtcNow;
            }
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
            return revision;
        }

        /// <summary>
        /// Get ReviewIds of Language corresponding Review linked by CrossLanguagePackageId
        /// </summary>
        /// <param name="crossLanguagePackageId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<string>> GetReviewIdsOfLanguageCorrespondingReviewAsync(string crossLanguagePackageId) {
            return await _apiRevisionsRepository.GetReviewIdsOfLanguageCorrespondingReviewAsync(crossLanguagePackageId);
        }

        /// <summary>
        /// Generate the Revision on a DevOps Pipeline
        /// </summary>
        /// <param name="review"></param>
        /// <param name="revisionId"></param>
        /// <param name="fileId"></param>
        /// <param name="fileName"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task GenerateAPIRevisionInExternalResource(ReviewListItemModel review, string revisionId, string fileId, string fileName, string language = null)
        {
            var languageService = _languageServices.Single(s => s.Name == language || s.Name == review.Language);
            var param = new APIRevisionGenerationPipelineParamModel()
            {
                FileID = fileId,
                ReviewID = review.Id,
                RevisionID = revisionId,
                FileName = fileName
            };
            if (!languageService.GeneratePipelineRunParams(param))
            {
                throw new Exception($"Failed to run pipeline for review: {param.ReviewID}, file: {param.FileName}");
            }

            var paramList = new List<APIRevisionGenerationPipelineParamModel>
            {
                param
            };

            await RunAPIRevisionGenerationPipeline(paramList, languageService.Name);
        }

        /// <summary>
        /// GetCodeLinesForDiff
        /// </summary>
        /// <param name="node"></param>
        /// <param name="curr"></param>
        /// <param name="codeFile"></param>
        /// <returns></returns>
        private (CodeLine[] htmlLines, CodeLine[] textLines) GetCodeLinesForDiff(TreeNode<CodeLine> node, TreeNode<InlineDiffLine<CodeLine>> curr, RenderedCodeFile codeFile)
        {
            (CodeLine[] htmlLines, CodeLine[] textLines) result = (new CodeLine[] { }, new CodeLine[] { });
            if (node != null)
            {
                if (node.IsLeaf)
                {
                    result.htmlLines = codeFile.GetDetachedLeafSectionLines(node);
                    result.textLines = codeFile.GetDetachedLeafSectionLines(node, renderType: RenderType.Text, skipDiff: true);

                    if (result.htmlLines.Count() > 0)
                    {
                        curr.WasDetachedLeafParent = true;
                    }
                }
                else
                {
                    result.htmlLines = result.textLines = node.Children.Select(x => new CodeLine(x.Data, nodeRef: x)).ToArray();
                }
            }
            return result;
        }
        private async Task<APIRevisionListItemModel> UpgradeAPIRevisionIfRequired(APIRevisionListItemModel revisionModel)
        {
            if (revisionModel == null)
            {
                return revisionModel;
            }
            var codeFileDetails = revisionModel.Files[0];
            if (_upgradeDisabledLangs.Contains(codeFileDetails.Language))
            {
                return revisionModel;
            }
            var languageService = LanguageServiceHelpers.GetLanguageService(codeFileDetails.Language, _languageServices);
            if (languageService != null && languageService.CanUpdate(codeFileDetails.VersionString))
            {
                await UpdateAPIRevisionAsync(revisionModel, languageService, false);
                revisionModel = await _apiRevisionsRepository.GetAPIRevisionAsync(revisionModel.Id);
            }
            else if (languageService != null && languageService.CanConvert(codeFileDetails.VersionString) &&  codeFileDetails.ParserStyle == ParserStyle.Flat)
            {
                // Convert to tree model only if current token file is in flat token model
                var codeFile = await _codeFileRepository.GetCodeFileFromStorageAsync(revisionModel.Id, codeFileDetails.FileId);
                if (codeFile != null && codeFile.ReviewLines.Count == 0)
                {
                    codeFile.ConvertToTreeTokenModel();                    
                    if (codeFile.ReviewLines.Count > 0)
                    {
                        await _codeFileRepository.UpsertCodeFileAsync(revisionModel.Id, codeFileDetails.FileId, codeFile);
                        codeFileDetails.VersionString = languageService.VersionString;
                        codeFileDetails.ParserStyle = ParserStyle.Tree;
                        codeFileDetails.IsConvertedTokenModel = true;
                        await _apiRevisionsRepository.UpsertAPIRevisionAsync(revisionModel);
                    }                    
                }
            }
            return revisionModel;
        }
    }
}
