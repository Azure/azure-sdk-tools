using Amazon.Runtime.Internal.Transform;
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
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly IHubContext<SignalRHub> _signalRHubContext;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IDevopsArtifactRepository _devopsArtifactRepository;
        private readonly IBlobOriginalsRepository _originalsRepository;
        private readonly INotificationManager _notificationManager;

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());


        public APIRevisionsManager(
            IAuthorizationService authorizationService,
            ICosmosReviewRepository reviewsRepository,
            ICosmosAPIRevisionsRepository reviewsRevisionsRepository,
            IHubContext<SignalRHub> signalRHubContext,
            IEnumerable<LanguageService> languageServices,
            IDevopsArtifactRepository devopsArtifctRepository,
            ICodeFileManager codeFileManager,
            IBlobCodeFileRepository codeFileRepository,
            IBlobOriginalsRepository originalsRepository,
            INotificationManager notificationManager)
        {
            _reviewsRepository = reviewsRepository;
            _apiRevisionsRepository = reviewsRevisionsRepository;
            _authorizationService = authorizationService;
            _signalRHubContext = signalRHubContext;
            _codeFileManager = codeFileManager;
            _codeFileRepository = codeFileRepository;
            _languageServices = languageServices;
            _devopsArtifactRepository = devopsArtifctRepository;
            _originalsRepository = originalsRepository;
            _notificationManager = notificationManager;
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query.
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public async Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(PageParams pageParams, APIRevisionsFilterAndSortParams filterAndSortParams)
        {
             return await _apiRevisionsRepository.GetAPIRevisionsAsync(pageParams, filterAndSortParams);
        }

        /// <summary>
        /// Retrieve Revisions for a particular Review from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="reviewId"></param> The Reviewid for which the revisions are to be retrieved
        /// <returns></returns>
        public async Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId)
        {
            return await _apiRevisionsRepository.GetAPIRevisionsAsync(reviewId);
        }

        /// <summary>
        /// Retrieve the latest Revisions for a particular Review from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="apiRevision"></param> The list of revisions can be supplied if available to avoid another call to the database
        /// <param name="apiRevisionType"></param>
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> GetLatestAPIRevisionsAsync(string reviewId, IEnumerable<APIRevisionListItemModel> apiRevision = null, APIRevisionType apiRevisionType = APIRevisionType.All)
        {
            var apiRevisions = (apiRevision == null) ? await _apiRevisionsRepository.GetAPIRevisionsAsync(reviewId) : apiRevision;
            if (apiRevisionType != APIRevisionType.All)
            {
                apiRevisions = apiRevisions.Where(r => r.APIRevisionType == apiRevisionType);
            }
            return apiRevisions.OrderByDescending(r => r.CreatedOn).FirstOrDefault();
        }

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="revisionId"></param> The RevisionId for which the revision is to be retrieved
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> GetAPIRevisionAsync(ClaimsPrincipal user, string revisionId)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            return await _apiRevisionsRepository.GetAPIRevisionAsync(revisionId);
        }

        /// <summary>
        /// Add new Approval or ApprovalReverted action to the ChangeHistory of a Revision
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <param name="apiRevisionId"></param>
        /// <param name="apiRevision"></param>
        /// <param name="notes"></param>
        /// <returns>true if review approval needs to be updated otherwise false</returns>
        public async Task<bool> ToggleAPIRevisionApprovalAsync(ClaimsPrincipal user, string id, string apiRevisionId = null, APIRevisionListItemModel apiRevision = null, string notes = "")
        {
            if (apiRevisionId == null && apiRevision == null)
            {
                throw new ArgumentException(message: "apiRevisionId and apiRevision cannot both be null");
            }

            bool updateReview = false;
            if (apiRevision == null)
            {
                apiRevision = await _apiRevisionsRepository.GetReviewRevisionAsync(apiRevisionId);
            }
            ReviewListItemModel review = await _reviewsRepository.GetReviewAsync(apiRevision.ReviewId);

            await ManagerHelpers.AssertApprover<APIRevisionListItemModel>(user, apiRevision, _authorizationService);
            var userId = user.GetGitHubLogin();
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
            await _signalRHubContext.Clients.Group(userId).SendAsync("ReceiveApprovalSelf", id, apiRevisionId, apiRevision.IsApproved);
            await _signalRHubContext.Clients.All.SendAsync("ReceiveApproval", id, apiRevisionId, userId, apiRevision.IsApproved);
            return updateReview;
        }

        /// <summary>
        /// Add API Revision to Review
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="name"></param>
        /// <param name="label"></param>
        /// <param name="fileStream"></param>
        /// <param name="language"></param>
        /// <param name="awaitComputeDiff"></param>
        /// <returns></returns>
        public async Task AddAPIRevisionAsync(
            ClaimsPrincipal user,
            string reviewId,
            string name,
            string label,
            Stream fileStream,
            string language = "",
            bool awaitComputeDiff = false)
        {
            var review = await _reviewsRepository.GetReviewAsync(reviewId);
            await AddAPIRevisionAsync(user, review, name, label, fileStream, language, awaitComputeDiff);
        }

        /// <summary>
        /// For reviews with collapsible sections (Swagger). Precomputs the line numbers of the headings with diff
        /// </summary>
        /// <param name="reviewId"></param>
        /// <param name="revision"></param>
        /// <returns></returns>
        public async Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, APIRevisionListItemModel revision)
        {
            var revisions = await _apiRevisionsRepository.GetAPIRevisionsAsync(reviewId);
            var latestRevisionCodeFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var latestRevisionHtmlLines = latestRevisionCodeFile.Render(false);
            var latestRevisionTextLines = latestRevisionCodeFile.RenderText(false);

            foreach (var rev in revisions)
            {
                // Calculate diff against previous revisions only. APIView only shows diff against revision lower than current one.
                if (rev.Id != revision.Id && rev.CreatedOn < revision.CreatedOn)
                {
                    var lineNumbersForHeadingOfSectionWithDiff = new HashSet<int>();
                    var earlierRevisionCodeFile = await _codeFileRepository.GetCodeFileAsync(rev, false);
                    var earlierRevisionHtmlLines = earlierRevisionCodeFile.RenderReadOnly(false);
                    var earlierRevisionTextLines = earlierRevisionCodeFile.RenderText(false);

                    var diffLines = InlineDiff.Compute(earlierRevisionTextLines, latestRevisionTextLines, earlierRevisionHtmlLines, latestRevisionHtmlLines);

                    foreach (var diffLine in diffLines)
                    {
                        if (diffLine.Kind == DiffLineKind.Unchanged && diffLine.Line.SectionKey != null && diffLine.OtherLine.SectionKey != null)
                        {
                            var latestRevisionRootNode = latestRevisionCodeFile.GetCodeLineSectionRoot((int)diffLine.Line.SectionKey);
                            var earlierRevisionRootNode = earlierRevisionCodeFile.GetCodeLineSectionRoot((int)diffLine.OtherLine.SectionKey);
                            var diffSectionRoot = ComputeSectionDiff(earlierRevisionRootNode, latestRevisionRootNode, earlierRevisionCodeFile, latestRevisionCodeFile);
                            if (latestRevisionCodeFile.ChildNodeHasDiff(diffSectionRoot))
                                lineNumbersForHeadingOfSectionWithDiff.Add((int)diffLine.Line.LineNumber);
                        }
                    }
                    if (rev.HeadingsOfSectionsWithDiff.ContainsKey(revision.Id))
                    {
                        rev.HeadingsOfSectionsWithDiff.Remove(revision.Id);
                    }
                    if (lineNumbersForHeadingOfSectionWithDiff.Any())
                    {
                        rev.HeadingsOfSectionsWithDiff.Add(revision.Id, lineNumbersForHeadingOfSectionWithDiff);
                    }
                }
                await _apiRevisionsRepository.UpsertAPIRevisionAsync(rev);
            }
        }

        /// <summary>
        /// Computed the diff for hidden (colapsible) API sections
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
        /// <param name="name"></param>
        /// <param name="label"></param>
        /// <param name="fileStream"></param>
        /// <param name="language"></param>
        /// <param name="awaitComputeDiff"></param>
        /// <returns></returns>
        public async Task AddAPIRevisionAsync(
            ClaimsPrincipal user,
            ReviewListItemModel review,
            string name,
            string label,
            Stream fileStream,
            string language,
            bool awaitComputeDiff = false)
        {
            var revision = new APIRevisionListItemModel();

            var codeFile = await _codeFileManager.CreateCodeFileAsync(
                revision.Id,
                name,
                fileStream,
                true,
                language);

            revision.ReviewId = review.Id;
            revision.PackageName = review.PackageName;
            revision.Language = review.Language;
            revision.Files.Add(codeFile);
            revision.CreatedBy = user.GetGitHubLogin();
            revision.Label = label;

            var languageService = language != null ? _languageServices.FirstOrDefault(l => l.Name == language) : _languageServices.FirstOrDefault(s => s.IsSupportedFile(name));
            // Run pipeline to generate the review if sandbox is enabled
            if (languageService != null && languageService.IsReviewGenByPipeline)
            {
                // Run offline review gen for review and reviewCodeFileModel
                await GenerateAPIRevisionInExternalResource(review, revision.Id, codeFile.FileId, name, language);
            }

            // auto subscribe revision creation user
            await _notificationManager.SubscribeAsync(review, user);
            await _reviewsRepository.UpsertReviewAsync(review);
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
            await _notificationManager.NotifySubscribersOnNewRevisionAsync(review, revision, user);

            if (!String.IsNullOrEmpty(review.Language) && review.Language == "Swagger")
            {
                if (awaitComputeDiff)
                {
                    await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.Id, revision);
                }
                else
                {
                    _ = Task.Run(async () => await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.Id, revision));
                }
            }
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
            var apiRevision = await _apiRevisionsRepository.GetReviewRevisionAsync(apiRevisionId);
            ManagerHelpers.AssertAPIRevisionDeletion(apiRevision);
            await ManagerHelpers.AssertAPIRevisionOwner(user, apiRevision, _authorizationService);
            await SoftDeleteAPIRevisionAsync(user, apiRevision);
        }

        /// <summary>
        /// Delete APIRevisions
        /// </summary>
        /// <param name="user"></param>
        /// <param name="apiRevision"></param>
        /// <returns></returns>
        public async Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, APIRevisionListItemModel apiRevision)
        {
            ManagerHelpers.AssertAPIRevisionDeletion(revision);
            await ManagerHelpers.AssertAPIRevisionOwner(user, revision, _authorizationService);
            var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(revision.ChangeHistory, APIRevisionChangeAction.Deleted, user.GetGitHubLogin());
            revision.ChangeHistory = changeUpdate.ChangeHistory;
            revision.IsDeleted = changeUpdate.ChangeStatus;

            await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
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
        /// Check if APIRevision is the Same
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="renderedCodeFile"></param>
        /// <returns></returns>
        public async Task<bool> IsAPIRevisionTheSame(APIRevisionListItemModel revision, RenderedCodeFile renderedCodeFile)
        {
            //This will compare and check if new code file content is same as revision in parameter
            var lastRevisionFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var lastRevisionTextLines = lastRevisionFile.RenderText(false, skipDiff: true);
            var fileTextLines = renderedCodeFile.RenderText(false, skipDiff: true);
            return lastRevisionTextLines.SequenceEqual(fileTextLines);
        }

        /// <summary>
        /// Update APIRevision
        /// </summary>
        /// <param name="revision"></param>
        /// <param name="languageService"></param>
        /// <param name="telemetryClient"></param>
        /// <returns></returns>
        public async Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision, LanguageService languageService, TelemetryClient telemetryClient)
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
                    // file.Name property has been repurposed to store package name and version string
                    // This is causing issue when updating review using latest parser since it expects Name field as file name
                    // We have added a new property FileName which is only set for new reviews
                    // All older reviews needs to be handled by checking review name field
                    var fileName = file.FileName ?? file.Name;
                    var codeFile = await languageService.GetCodeFileAsync(fileName, fileOriginal, false);
                    await _codeFileRepository.UpsertCodeFileAsync(revision.Id, file.FileId, codeFile);
                    // update only version string
                    file.VersionString = codeFile.VersionString;
                    await _apiRevisionsRepository.UpsertAPIRevisionAsync(revision);
                }
                catch (Exception ex)
                {
                    telemetryClient.TrackTrace("Failed to update revision " + revision.Id);
                    telemetryClient.TrackException(ex);
                }
            }
        }
        
        /// <summary>
        /// SoftDelete APIRevision if its not been updated after many months
        /// </summary>
        /// <param name="archiveAfterMonths"></param>
        /// <returns></returns>
        public async Task AutoArchiveAPIRevisions(int archiveAfterMonths)
        {
            var lastUpdatedDate = DateTime.Now.Subtract(TimeSpan.FromDays(archiveAfterMonths * 30));
            var manualRevisions = await _apiRevisionsRepository.GetAPIRevisionsAsync(lastUpdatedOn: lastUpdatedDate, apiRevisionType:  APIRevisionType.Manual);

            // Find all inactive reviews
            foreach (var apiRevision in manualRevisions)
            {
                var requestTelemetry = new RequestTelemetry { Name = "Archiving Revision " + apiRevision.Id };
                var operation = _telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    await SoftDeleteAPIRevisionAsync(apiRevision: apiRevision, notes: "Auto archived");
                    await Task.Delay(500);
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e);
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }
        }

        private async Task SoftDeleteAPIRevisionAsync(APIRevisionListItemModel apiRevision, string userName = "azure-sdk", string notes = "")
        {
            if (!apiRevision.IsDeleted)
            {
                var changeUpdate = ChangeHistoryHelpers.UpdateBinaryChangeAction(
                     changeHistory: apiRevision.ChangeHistory, action: APIRevisionChangeAction.Deleted, user: userName, notes: notes);

                apiRevision.ChangeHistory = changeUpdate.ChangeHistory;
                apiRevision.IsDeleted = changeUpdate.ChangeStatus;

                await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            }
        }

        /// <summary>
        /// CreateAPIRevisionAsync
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <param name="apiRevisionType"></param>
        /// <param name="label"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <param name="originalName"></param>
        /// <returns></returns>
        public async Task<APIRevisionListItemModel> CreateAPIRevisionAsync(ClaimsPrincipal user, string reviewId, APIRevisionType apiRevisionType, string label, MemoryStream memoryStream, CodeFile codeFile, string originalName = null)
        {
            var apiRevision = new APIRevisionListItemModel()
            {
                ReviewId = reviewId,
                PackageName = codeFile.PackageName,
                Language = codeFile.Language,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = user.GetGitHubLogin(),
                APIRevisionType = apiRevisionType,
                ChangeHistory = new List<APIRevisionChangeHistoryModel>()
                {
                    new APIRevisionChangeHistoryModel()
                    {
                        ChangeAction = APIRevisionChangeAction.Created,
                        ChangedBy = user.GetGitHubLogin(),
                        ChangedOn = DateTime.UtcNow
                    }
                },
                Label = label
            };

            var apiRevisionCodeFile = await _codeFileManager.CreateReviewCodeFileModel(apiRevisionId: apiRevision.Id, memoryStream: memoryStream, codeFile: codeFile);
            apiRevision.Files.Add(apiRevisionCodeFile);
            if (!string.IsNullOrEmpty(originalName))
            {
                apiRevisionCodeFile.FileName = originalName;
            }
            await _apiRevisionsRepository.UpsertAPIRevisionAsync(apiRevision);
            return apiRevision;
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
    }
}
