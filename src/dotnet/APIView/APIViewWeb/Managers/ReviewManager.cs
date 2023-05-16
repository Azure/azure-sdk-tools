// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIView.Model;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb.Managers
{
    public class ReviewManager : IReviewManager
    {

        private readonly IAuthorizationService _authorizationService;

        private readonly ICosmosReviewRepository _reviewsRepository;

        private readonly IBlobCodeFileRepository _codeFileRepository;

        private readonly IBlobOriginalsRepository _originalsRepository;

        private readonly ICosmosCommentsRepository _commentsRepository;

        private readonly IEnumerable<LanguageService> _languageServices;

        private readonly INotificationManager _notificationManager;

        private readonly IDevopsArtifactRepository _devopsArtifactRepository;

        private readonly IPackageNameManager _packageNameManager;

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public ReviewManager (
            IAuthorizationService authorizationService, ICosmosReviewRepository reviewsRepository,
            IBlobCodeFileRepository codeFileRepository, IBlobOriginalsRepository originalsRepository,
            ICosmosCommentsRepository commentsRepository, IEnumerable<LanguageService> languageServices,
            INotificationManager notificationManager, IDevopsArtifactRepository devopsClient,
            IPackageNameManager packageNameManager)
        {
            _authorizationService = authorizationService;
            _reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
            _commentsRepository = commentsRepository;
            _languageServices = languageServices;
            _notificationManager = notificationManager;
            _devopsArtifactRepository = devopsClient;
            _packageNameManager = packageNameManager;
        }

        public async Task<ReviewModel> CreateReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool runAnalysis, string langauge, bool awaitComputeDiff = false)
        {
            var review = new ReviewModel
            {
                Author = user.GetGitHubLogin(),
                CreationDate = DateTime.UtcNow,
                RunAnalysis = runAnalysis,
                Name = fileStream != null? originalName : Path.GetFileName(originalName),
                FilterType = ReviewType.Manual
            };
            await AddRevisionAsync(user, review, originalName, label, fileStream, langauge, awaitComputeDiff);
            return review;
        }

        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool closed, string language, string packageName = null, ReviewType filterType = ReviewType.Manual)
        {
            return _reviewsRepository.GetReviewsAsync(closed, language, packageName: packageName, filterType: filterType);
        }

        public async Task<IEnumerable<ReviewModel>> GetReviewsAsync(string ServiceName, string PackageName, IEnumerable<ReviewType> filterTypes)
        {
            return await _reviewsRepository.GetReviewsAsync(ServiceName, PackageName, filterTypes);
        }

        public async Task<IEnumerable<string>> GetReviewPropertiesAsync(string propertyName)
        {
            return await _reviewsRepository.GetReviewFirstLevelPropertiesAsync(propertyName);
        }

        public async Task<IEnumerable<ReviewModel>> GetRequestedReviews(string userName)
        {
            return await _reviewsRepository.GetRequestedReviews(userName);
        }

        public async Task<(IEnumerable<ReviewModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage)> GetPagedReviewsAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, IEnumerable<int> filterTypes, bool? isApproved, int offset, int limit, string orderBy)
        {
            var result = await _reviewsRepository.GetReviewsAsync(search, languages, isClosed, filterTypes, isApproved, offset, limit, orderBy);

            // Calculate and add Previous and Next and Current page to the returned result
            var totalPages = (int)Math.Ceiling(result.TotalCount / (double)limit);
            var currentPage = offset == 0 ? 1 : offset / limit + 1;

            (IEnumerable<ReviewModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage) resultToReturn = (
                result.Reviews, result.TotalCount, TotalPages: totalPages,
                CurrentPage: currentPage,
                PreviousPage: currentPage == 1 ? null : currentPage - 1,
                NextPage: currentPage >= totalPages ? null : currentPage + 1
            );
            return resultToReturn;
        }

        public async Task DeleteReviewAsync(ClaimsPrincipal user, string id)
        {
            var reviewModel = await _reviewsRepository.GetReviewAsync(id);
            await AssertReviewOwnerAsync(user, reviewModel);
            AssertReviewDeletion(reviewModel);

            await _reviewsRepository.DeleteReviewAsync(reviewModel);

            foreach (var revision in reviewModel.Revisions)
            {
                foreach (var file in revision.Files)
                {
                    if (file.HasOriginal)
                    {
                        await _originalsRepository.DeleteOriginalAsync(file.ReviewFileId);
                    }

                    await _codeFileRepository.DeleteCodeFileAsync(revision.RevisionId, file.ReviewFileId);
                }
            }

            await _commentsRepository.DeleteCommentsAsync(id);
        }

        public async Task<ReviewModel> GetReviewAsync(ClaimsPrincipal user, string id)
        {
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var review = await _reviewsRepository.GetReviewAsync(id);
            review.UpdateAvailable = IsUpdateAvailable(review);

            // Handle old model
#pragma warning disable CS0618 // Type or member is obsolete
            if (review.Revisions.Count == 0 && review.Files.Count == 1)
            {
                var file = review.Files[0];
#pragma warning restore CS0618 // Type or member is obsolete
                review.Revisions.Add(new ReviewRevisionModel()
                {
                    RevisionId = file.ReviewFileId,
                    CreationDate = file.CreationDate,
                    Files =
                    {
                        file
                    }
                });
            }

            if (review.PackageName != null && review.PackageDisplayName == null)
            {
                var p = await _packageNameManager.GetPackageDetails(review.PackageName);
                review.PackageDisplayName = p?.DisplayName;
                review.ServiceName = p?.ServiceName;
            }

            //If current review doesn't have package approved for first release status then check if package is approved for any reviews for the same package.
            if (!review.IsApprovedForFirstRelease)
            {
                var reviews = await _reviewsRepository.GetApprovedForFirstReleaseReviews(review.Language, review.PackageName);
                if (reviews.Any())
                {
                    var nameApprovedReview = reviews.First();
                    review.ApprovedForFirstReleaseBy = nameApprovedReview.ApprovedForFirstReleaseBy;
                    review.ApprovalDate = nameApprovedReview.ApprovedForFirstReleaseOn;
                    review.IsApprovedForFirstRelease = true;
                }
                else
                {
                    // Mark package as approved if review is already approved. Copy approval details from review approval.
                    reviews = await _reviewsRepository.GetApprovedReviews(review.Language, review.PackageName);
                    if (reviews.Any())
                    {
                        var approvedRevision = reviews.First(r => r.Revisions.Any(rev => rev.IsApproved)).Revisions.First(rev => rev.IsApproved);
                        review.ApprovedForFirstReleaseBy = approvedRevision.Approvers.FirstOrDefault();
                        review.ApprovedForFirstReleaseOn = reviews.First().ApprovalDate;
                        review.IsApprovedForFirstRelease = true;
                    }
                }
            }
            return review;
        }

        public async Task AddRevisionAsync(
            ClaimsPrincipal user,
            string reviewId,
            string name,
            string label,
            Stream fileStream,
            string language = "",
            bool awaitComputeDiff = false)
        {
            var review = await GetReviewAsync(user, reviewId);
            await AssertAutomaticReviewModifier(user, review);
            await AddRevisionAsync(user, review, name, label, fileStream, language, awaitComputeDiff);
        }

        public async Task<CodeFile> CreateCodeFile(
            string originalName,
            Stream fileStream,
            bool runAnalysis,
            MemoryStream memoryStream,
            string language = null)
        {
            var languageService = _languageServices.FirstOrDefault(s => (language != null ? s.Name == language : s.IsSupportedFile(originalName)));
            if (fileStream != null)
            {
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
            }            
            CodeFile codeFile = null;
            if (languageService.IsReviewGenByPipeline)
            {
                codeFile = languageService.GetReviewGenPendingCodeFile(originalName);
            }
            else
            {
                codeFile = await languageService.GetCodeFileAsync(
                originalName,
                memoryStream,
                runAnalysis);
            }
            return codeFile;
        }

        public async Task<ReviewCodeFileModel> CreateReviewCodeFileModel(string revisionId, MemoryStream memoryStream, CodeFile codeFile)
        {
            var reviewCodeFileModel = new ReviewCodeFileModel
            {
                HasOriginal = true,
            };

            InitializeFromCodeFile(reviewCodeFileModel, codeFile);
            if (memoryStream != null)
            {
                memoryStream.Position = 0;
                await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.ReviewFileId, memoryStream);
            }
            await _codeFileRepository.UpsertCodeFileAsync(revisionId, reviewCodeFileModel.ReviewFileId, codeFile);
            return reviewCodeFileModel;
        }

        public async Task DeleteRevisionAsync(ClaimsPrincipal user, string id, string revisionId)
        {
            var review = await GetReviewAsync(user, id);
            AssertReviewDeletion(review);
            var revision = review.Revisions.Single(r => r.RevisionId == revisionId);
            await AssertRevisionOwner(user, revision);

            if (review.Revisions.Count < 2)
            {
                return;
            }
            review.Revisions.Remove(revision);
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task UpdateRevisionLabelAsync(ClaimsPrincipal user, string id, string revisionId, string label)
        {
            var review = await GetReviewAsync(user, id);
            var revision = review.Revisions.Single(r => r.RevisionId == revisionId);
            await AssertRevisionOwner(user, revision);
            revision.Label = label;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task ToggleIsClosedAsync(ClaimsPrincipal user, string id)
        {
            var review = await GetReviewAsync(user, id);
            review.IsClosed = !review.IsClosed;
            if (review.FilterType == ReviewType.Automatic)
            {
                throw new AuthorizationFailedException();
            }
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task ToggleApprovalAsync(ClaimsPrincipal user, string id, string revisionId)
        {
            var review = await GetReviewAsync(user, id);
            var revision = review.Revisions.Single(r => r.RevisionId == revisionId);
            await AssertApprover(user, revision);
            var userId = user.GetGitHubLogin();
            if (revision.Approvers.Contains(userId))
            {
                //Revert approval
                revision.Approvers.Remove(userId);
            }
            else
            {
                //Approve revision
                revision.Approvers.Add(userId);
                review.ApprovalDate = DateTime.Now;
            }
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task ApprovePackageNameAsync(ClaimsPrincipal user, string id)
        {
            var review = await GetReviewAsync(user, id);
            await AssertApprover(user, review.Revisions.Last());
            review.ApprovedForFirstReleaseBy = user.GetGitHubLogin();
            review.ApprovedForFirstReleaseOn = DateTime.Now;
            review.IsApprovedForFirstRelease = true;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        public async Task<bool> IsReviewSame(ReviewRevisionModel revision, RenderedCodeFile renderedCodeFile)
        {
            //This will compare and check if new code file content is same as revision in parameter
            var lastRevisionFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var lastRevisionTextLines = lastRevisionFile.RenderText(false, skipDiff: true);
            var fileTextLines = renderedCodeFile.RenderText(false, skipDiff: true);
            return lastRevisionTextLines.SequenceEqual(fileTextLines);
        }

        public async Task<ReviewRevisionModel> CreateMasterReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool compareAllRevisions)
        {
            //Generate code file from new uploaded package
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFile(originalName, fileStream, false, memoryStream);
            return await CreateMasterReviewAsync(user, codeFile, originalName, label, memoryStream, compareAllRevisions);
        }

        public async Task UpdateReviewBackground(HashSet<string> updateDisabledLanguages, int backgroundBatchProcessCount)
        {
            foreach(var language in LanguageService.SupportedLanguages)
            {
                if (updateDisabledLanguages.Contains(language))
                {
                    _telemetryClient.TrackTrace("Background task to update API review at startup is disabled for langauge " + language);
                    continue;
                }

                var languageService = GetLanguageService(language);
                if (languageService == null)
                    return;

                // If review is updated using devops pipeline then batch process update review requests
                if (languageService.IsReviewGenByPipeline)
                {
                    await UpdateReviewsUsingPipeline(language, languageService, backgroundBatchProcessCount);
                }
                else
                {
                    var reviews = await _reviewsRepository.GetReviewsAsync(false, language, fetchAllPages: true);
                    foreach (var review in reviews.Where(r => IsUpdateAvailable(r)))
                    {
                        var requestTelemetry = new RequestTelemetry { Name = "Updating Review " + review.ReviewId };
                        var operation = _telemetryClient.StartOperation(requestTelemetry);
                        try
                        {
                            await Task.Delay(500);
                            await UpdateReviewAsync(review, languageService);
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
            }            
        }

        // Languages that full ysupport sandboxing updates reviews using Azure devops pipeline
        // We should batch all eligible reviews to avoid a pipeline run storm
        private async Task UpdateReviewsUsingPipeline(string language, LanguageService languageService, int backgroundBatchProcessCount)
        {
            var reviews = await _reviewsRepository.GetReviewsAsync(false, language, fetchAllPages: true);
            var paramList = new List<ReviewGenPipelineParamModel>();

            foreach(var review in reviews)
            {
                foreach (var revision in review.Revisions.Reverse())
                {
                    foreach (var file in revision.Files)
                    {
                        //Don't include current revision if file is not required to be updated.
                        // E.g. json token file is uploaded for a language, specific revision was already upgraded.
                        if (!file.HasOriginal || file.FileName == null || !languageService.IsSupportedFile(file.FileName) || !languageService.CanUpdate(file.VersionString))
                        {
                            continue;
                        }

                        _telemetryClient.TrackTrace($"Updating review: {review.ReviewId}, revision: {revision.RevisionId}");
                        paramList.Add(new ReviewGenPipelineParamModel()
                        {
                            FileID = file.ReviewFileId,
                            ReviewID = review.ReviewId,
                            RevisionID = revision.RevisionId,
                            FileName = Path.GetFileName(file.FileName)
                        });
                    }
                }

                // This should be changed to configurable batch count
                if (paramList.Count >= backgroundBatchProcessCount)
                {
                    _telemetryClient.TrackTrace($"Running pipeline to update reviews for {language} with batch size {paramList.Count}");
                    await RunReviewGenPipeline(paramList, languageService.Name);
                    // Delay of 10 minute before starting next batch
                    // We should try to increase the number of revisions in the batch than number of runs.
                    await Task.Delay(600000);
                    paramList.Clear();
                }                
            }

            if (paramList.Count > 0)
            {
                _telemetryClient.TrackTrace($"Running pipeline to update reviews for {language} with batch size {paramList.Count}");
                await RunReviewGenPipeline(paramList, languageService.Name);
            }
        }

        public async Task<CodeFile> GetCodeFile(string repoName,
            string buildId,
            string artifactName,
            string packageName,
            string originalFileName,
            string codeFileName,
            MemoryStream originalFileStream,
            string baselineCodeFileName = "",
            MemoryStream baselineStream = null,
            string project = "public"
            )
        {
            Stream stream = null;
            CodeFile codeFile = null;
            if (string.IsNullOrEmpty(codeFileName))
            {
                // backward compatibility until all languages moved to sandboxing of codefile to pipeline
                stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, originalFileName, format: "file", project: project);
                codeFile = await CreateCodeFile(Path.GetFileName(originalFileName), stream, false, originalFileStream);
            }
            else
            {
                stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, packageName, format: "zip", project: project);
                var archive = new ZipArchive(stream);
                foreach (var entry in archive.Entries)
                {
                    var fileName = Path.GetFileName(entry.Name);
                    if (fileName == originalFileName)
                    {
                        await entry.Open().CopyToAsync(originalFileStream);
                    }

                    if (fileName == codeFileName)
                    {
                        codeFile = await CodeFile.DeserializeAsync(entry.Open());
                    }
                    else if (fileName == baselineCodeFileName)
                    {
                        await entry.Open().CopyToAsync(baselineStream);
                    }
                }
            }

            return codeFile;
        }

        public async Task<ReviewRevisionModel> CreateApiReview(
            ClaimsPrincipal user,
            string buildId,
            string artifactName,
            string originalFileName,
            string label,
            string repoName,
            string packageName,
            string codeFileName,
            bool compareAllRevisions,
            string project
            )
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await GetCodeFile(repoName, buildId, artifactName, packageName, originalFileName, codeFileName, memoryStream, project: project);
            return await CreateMasterReviewAsync(user, codeFile, originalFileName, label, memoryStream, compareAllRevisions);
        }

        public async Task<List<ServiceGroupModel>> GetReviewsByServicesAsync(ReviewType filterType)
        {
            SortedDictionary<string, ServiceGroupModel> response = new();
            var reviews = await _reviewsRepository.GetReviewsAsync(false, "All", filterType: filterType);
            foreach (var review in reviews)
            {
                var packageDisplayName = review.PackageDisplayName ?? "Other";
                var serviceName = review.ServiceName ?? "Other";
                if (!response.ContainsKey(serviceName))
                {
                    response[serviceName] = new ServiceGroupModel()
                    {
                        ServiceName = serviceName
                    };
                }

                var packageDict = response[serviceName].packages;
                if (!packageDict.ContainsKey(packageDisplayName))
                {
                    packageDict[packageDisplayName] = new PackageGroupModel()
                    {
                        PackageDisplayName = packageDisplayName
                    };
                }
                packageDict[packageDisplayName].reviews.Add(new ReviewDisplayModel(review));
            }
            return response.Values.ToList();
        }

        public async Task AutoArchiveReviews(int archiveAfterMonths)
        {
            var reviews = await _reviewsRepository.GetReviewsAsync(false, "All", filterType: ReviewType.Manual, fetchAllPages: true);
            // Find all inactive reviews
            reviews = reviews.Where(r => r.LastUpdated.AddMonths(archiveAfterMonths) < DateTime.Now);
            foreach (var review in reviews)
            {
                var requestTelemetry = new RequestTelemetry { Name = "Archiving Review " + review.ReviewId };
                var operation = _telemetryClient.StartOperation(requestTelemetry);
                try
                {
                    review.IsClosed = true;
                    await _reviewsRepository.UpsertReviewAsync(review);
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

        public async Task UpdateReviewCodeFiles(string repoName, string buildId, string artifact, string project)
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
                var revisionId = reviewDetails[2];
                var codeFile = await CodeFile.DeserializeAsync(entry.Open());

                // Update code file with one downloaded from pipeline
                var review = await _reviewsRepository.GetReviewAsync(reviewId);
                if (review != null)
                {
                    var revision = review.Revisions.SingleOrDefault(review => review.RevisionId == revisionId);
                    if (revision != null)
                    {
                        await _codeFileRepository.UpsertCodeFileAsync(revisionId, revision.SingleFile.ReviewFileId, codeFile);
                        var file = revision.Files.FirstOrDefault();
                        file.VersionString = codeFile.VersionString;
                        file.PackageName = codeFile.PackageName;
                        await _reviewsRepository.UpsertReviewAsync(review);

                        // Trigger diff calculation using updated code file from sandboxing pipeline
                        await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.ReviewId, revision);
                    }
                }
            }
        }

        public async Task RequestApproversAsync(ClaimsPrincipal User, string ReviewId, HashSet<string> reviewers)
        {
            var review = await GetReviewAsync(User, ReviewId);
            review.RequestedReviewers = reviewers;
            review.ApprovalRequestedOn = DateTime.Now;
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        /// <summary>
        /// Get the LineNumbers of the Heading that have diff changes in their sections
        /// </summary>
        public async Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, ReviewRevisionModel revision)
        {
            var review = await _reviewsRepository.GetReviewAsync(reviewId);
            var latestRevisionCodeFile = await _codeFileRepository.GetCodeFileAsync(revision, false);
            var latestRevisionHtmlLines = latestRevisionCodeFile.Render(false);
            var latestRevisionTextLines = latestRevisionCodeFile.RenderText(false);

            foreach (var rev in review.Revisions)
            {
                // Calculate diff against previous revisions only. APIView only shows diff against revision lower than current one.
                if (rev.RevisionId != revision.RevisionId && rev.RevisionNumber < revision.RevisionNumber)
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
                    if (rev.HeadingsOfSectionsWithDiff.ContainsKey(revision.RevisionId))
                    {
                        rev.HeadingsOfSectionsWithDiff.Remove(revision.RevisionId);
                    }
                    rev.HeadingsOfSectionsWithDiff.Add(revision.RevisionId, lineNumbersForHeadingOfSectionWithDiff);
                }
            }
            await _reviewsRepository.UpsertReviewAsync(review);
        }

        /// <summary>
        /// Computes diff for each level of the tree hierachy of a section
        /// </summary>
        public TreeNode<InlineDiffLine<CodeLine>> ComputeSectionDiff(TreeNode<CodeLine> before, TreeNode<CodeLine> after, RenderedCodeFile beforeFile, RenderedCodeFile afterFile)
        {
            var rootDiff = new InlineDiffLine<CodeLine>(before.Data, after.Data, DiffLineKind.Unchanged);
            var resultRoot = new TreeNode<InlineDiffLine<CodeLine>>(rootDiff);

            var
                queue = new Queue<(TreeNode<CodeLine> before, TreeNode<CodeLine> after, TreeNode<InlineDiffLine<CodeLine>> current)>();

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

        public async Task<bool> IsApprovedForFirstRelease(string language, string packageName)
        {
            var reviews = await _reviewsRepository.GetApprovedForFirstReleaseReviews(language, packageName);
            if (!reviews.Any())
            {
                reviews = await _reviewsRepository.GetApprovedReviews(language, packageName);
            }
            return reviews.Any();
        }


        private async Task UpdateReviewAsync(ReviewModel review, LanguageService languageService)
        {
            foreach (var revision in review.Revisions.Reverse())
            {
                foreach (var file in revision.Files)
                {
                    if (!file.HasOriginal || !languageService.CanUpdate(file.VersionString))
                    {
                        continue;
                    }

                    try
                    {
                        var fileOriginal = await _originalsRepository.GetOriginalAsync(file.ReviewFileId);
                        // file.Name property has been repurposed to store package name and version string
                        // This is causing issue when updating review using latest parser since it expects Name field as file name
                        // We have added a new property FileName which is only set for new reviews
                        // All older reviews needs to be handled by checking review name field
                        var fileName = file.FileName ?? (Path.HasExtension(review.Name) ? review.Name : file.Name);
                        var codeFile = await languageService.GetCodeFileAsync(fileName, fileOriginal, review.RunAnalysis);
                        await _codeFileRepository.UpsertCodeFileAsync(revision.RevisionId, file.ReviewFileId, codeFile);
                        // update only version string
                        file.VersionString = codeFile.VersionString;
                        await _reviewsRepository.UpsertReviewAsync(review);
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackTrace("Failed to update review " + review.ReviewId);
                        _telemetryClient.TrackException(ex);
                    }
                }
            }
        }

        private async Task AddRevisionAsync(
            ClaimsPrincipal user,
            ReviewModel review,
            string name,
            string label,
            Stream fileStream,
            string language,
            bool awaitComputeDiff = false)
        {
            var revision = new ReviewRevisionModel();

            var codeFile = await CreateFileAsync(
                revision.RevisionId,
                name,
                fileStream,
                review.RunAnalysis,
                language);

            // Set revision name using file name in case input is a URL(Input is either fielstream or URL)
            if (fileStream == null)
            {
                revision.Name = Path.GetFileName(name);
            }
            revision.Files.Add(codeFile);
            revision.Author = user.GetGitHubLogin();
            revision.Label = label;
            review.Revisions.Add(revision);

            if (review.PackageName != null)
            {
                var p = await _packageNameManager.GetPackageDetails(review.PackageName);
                review.PackageDisplayName = p?.DisplayName ?? review.PackageDisplayName;
                review.ServiceName = p?.ServiceName ?? review.ServiceName;
            }

            var languageService = language != null ? _languageServices.FirstOrDefault( l=> l.Name == language) : _languageServices.FirstOrDefault(s => s.IsSupportedFile(name));
            // Run pipeline to generate the review if sandbox is enabled
            if (languageService != null && languageService.IsReviewGenByPipeline)
            {
                // Run offline review gen for review and reviewCodeFileModel
                await GenerateReviewOffline(review, revision.RevisionId, codeFile.ReviewFileId, name, language);
            }

            // auto subscribe revision creation user
            await _notificationManager.SubscribeAsync(review, user);
            await _reviewsRepository.UpsertReviewAsync(review);
            await _notificationManager.NotifySubscribersOnNewRevisionAsync(revision, user);
            if (awaitComputeDiff)
            {
                await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.ReviewId, revision);
            }
            else
            {
                _ = Task.Run(async () => await GetLineNumbersOfHeadingsOfSectionsWithDiff(review.ReviewId, revision));
            }
        }

        private async Task<ReviewCodeFileModel> CreateFileAsync(
            string revisionId,
            string originalName,
            Stream fileStream,
            bool runAnalysis,
            string language)
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFile(originalName, fileStream, runAnalysis, memoryStream, language);
            var reviewCodeFileModel = await CreateReviewCodeFileModel(revisionId, memoryStream, codeFile);
            reviewCodeFileModel.FileName = originalName;
            return reviewCodeFileModel;
        }

        private void InitializeFromCodeFile(ReviewCodeFileModel file, CodeFile codeFile)
        {
            file.Language = codeFile.Language;
            file.LanguageVariant = codeFile.LanguageVariant;
            file.VersionString = codeFile.VersionString;
            file.Name = codeFile.Name;
            file.PackageName = codeFile.PackageName;
        }

        private LanguageService GetLanguageService(string language)
        {
            return _languageServices.FirstOrDefault(service => service.Name == language);
        }

        private void  AssertReviewDeletion(ReviewModel reviewModel)
        {
            // We allow deletion of manual API review only.
            // Server side assertion to ensure we are not processing any requests to delete automatic and PR API review
            if (reviewModel.FilterType != ReviewType.Manual)
            {
                throw new UnDeletableReviewException();
            }
        }

        private async Task AssertReviewOwnerAsync(ClaimsPrincipal user, ReviewModel reviewModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, reviewModel, new[] { ReviewOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private async Task AssertRevisionOwner(ClaimsPrincipal user, ReviewRevisionModel revisionModel)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                revisionModel,
                new[] { RevisionOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private async Task AssertApprover(ClaimsPrincipal user, ReviewRevisionModel revisionModel)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                revisionModel,
                new[] { ApproverRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private bool IsUpdateAvailable(ReviewModel review)
        {
            return review.Revisions
               .SelectMany(r => r.Files)
               .Any(f => f.HasOriginal && GetLanguageService(f.Language)?.CanUpdate(f.VersionString) == true);
        }

        private async Task<ReviewRevisionModel> CreateMasterReviewAsync(ClaimsPrincipal user, CodeFile codeFile, string originalName, string label, MemoryStream memoryStream, bool compareAllRevisions)
        {
            var renderedCodeFile = new RenderedCodeFile(codeFile);

            //Get current master review for package and language
            var review = await _reviewsRepository.GetMasterReviewForPackageAsync(codeFile.Language, codeFile.PackageName);
            var createNewRevision = true;
            ReviewRevisionModel reviewRevision = null;
            if (review != null)
            {
                // Delete pending revisions if it is not in approved state and if it doesn't have any comments before adding new revision
                // This is to keep only one pending revision since last approval or from initial review revision
                var lastRevision = review.Revisions.LastOrDefault();
                var comments = await _commentsRepository.GetCommentsAsync(review.ReviewId);
                while (lastRevision.Approvers.Count == 0 &&
                       review.Revisions.Count > 1 &&
                       !await IsReviewSame(lastRevision, renderedCodeFile) &&
                       !comments.Any(c => lastRevision.RevisionId == c.RevisionId))
                {
                    review.Revisions.Remove(lastRevision);
                    lastRevision = review.Revisions.LastOrDefault();
                }
                // We should compare against only latest revision when calling this API from scheduled CI runs
                // But any manual pipeline run at release time should compare against all approved revisions to ensure hotfix release doesn't have API change
                // If review surface doesn't match with any approved revisions then we will create new revision if it doesn't match pending latest revision
                if (compareAllRevisions)
                {
                    foreach (var approvedRevision in review.Revisions.Where(r => r.IsApproved).Reverse())
                    {
                        if (await IsReviewSame(approvedRevision, renderedCodeFile))
                        {
                            return approvedRevision;
                        }
                    }
                }

                if (await IsReviewSame(lastRevision, renderedCodeFile))
                {
                    reviewRevision = lastRevision;
                    createNewRevision = false;
                }
            }
            else
            {
                // Package and language combination doesn't have automatically created review. Create a new review.
                review = new ReviewModel
                {
                    Author = user.GetGitHubLogin(),
                    CreationDate = DateTime.UtcNow,
                    RunAnalysis = false,
                    Name = originalName,
                    FilterType = ReviewType.Automatic
                };
            }

            // Check if user is authorized to modify automatic review
            await AssertAutomaticReviewModifier(user, review);
            if (createNewRevision)
            {
                // Update or insert review with new revision
                reviewRevision = new ReviewRevisionModel()
                {
                    Author = user.GetGitHubLogin(),
                    Label = label
                };
                var reviewCodeFileModel = await CreateReviewCodeFileModel(reviewRevision.RevisionId, memoryStream, codeFile);
                reviewCodeFileModel.FileName = originalName;
                reviewRevision.Files.Add(reviewCodeFileModel);
                review.Revisions.Add(reviewRevision);
            }

            // Check if review can be marked as approved if another review with same surface level is in approved status
            if (review.Revisions.Last().Approvers.Count() == 0)
            {
                var matchingApprovedRevision = await FindMatchingApprovedRevision(review);
                if (matchingApprovedRevision != null)
                {
                    foreach (var approver in matchingApprovedRevision.Approvers)
                    {
                        review.Revisions.Last().Approvers.Add(approver);
                    }
                }
            }
            await _reviewsRepository.UpsertReviewAsync(review);
            return reviewRevision;
        }

        private async Task AssertAutomaticReviewModifier(ClaimsPrincipal user, ReviewModel reviewModel)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                reviewModel,
                new[] { AutoReviewModifierRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        private async Task<ReviewRevisionModel> FindMatchingApprovedRevision(ReviewModel review)
        {
            var revisionModel = review.Revisions.LastOrDefault();
            var revisionFile = revisionModel.Files.FirstOrDefault();
            var codeFile = await _codeFileRepository.GetCodeFileAsync(revisionModel);

            // Get manual reviews to check if a matching review is in approved state
            var reviews = await _reviewsRepository.GetReviewsAsync(false, revisionFile.Language, revisionFile.PackageName, ReviewType.Manual);
            var prReviews = await _reviewsRepository.GetReviewsAsync(false, revisionFile.Language, revisionFile.PackageName, ReviewType.PullRequest);
            reviews = reviews.Concat(prReviews);
            foreach (var r in reviews)
            {
                var approvedRevision = r.Revisions.Where(r => r.IsApproved).LastOrDefault();
                if (approvedRevision != null)
                {
                    var isReviewSame = await IsReviewSame(approvedRevision, codeFile);
                    if (isReviewSame)
                    {
                        return approvedRevision;
                    }
                }
            }
            return null;
        }

        private async Task GenerateReviewOffline(ReviewModel review, string revisionId, string fileId, string fileName, string language = null)
        {
            var languageService = _languageServices.Single(s => s.Name == language || s.Name == review.Language);
            var revision = review.Revisions.FirstOrDefault(r => r.RevisionId == revisionId);
            var param = new ReviewGenPipelineParamModel()
            {
                FileID = fileId,
                ReviewID = review.ReviewId,
                RevisionID = revisionId,
                FileName = fileName
            };
            if (!languageService.GeneratePipelineRunParams(param))
            {
                throw new Exception($"Failed to run pipeline for review: {param.ReviewID}, file: {param.FileName}");
            }

            var paramList = new List<ReviewGenPipelineParamModel>
            {
                param
            };

            await RunReviewGenPipeline(paramList, languageService.Name);
        }

        private async Task RunReviewGenPipeline(List<ReviewGenPipelineParamModel> reviewGenParams, string language)
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
