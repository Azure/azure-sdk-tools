using CloneAPIViewDB;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Text;


var config = new ConfigurationBuilder()
    .AddEnvironmentVariables(prefix: "APIVIEW_")
    .AddUserSecrets(typeof(Program).Assembly)
    .Build();

var cosmosClient = new CosmosClient(config["CosmosConnectionString"]);
var reviewsContainerOld = cosmosClient.GetContainer("APIView", "Reviews");
var commentsContainerOld = cosmosClient.GetContainer("APIView", "Comments");
var prContainerOld = cosmosClient.GetContainer("APIView", "PullRequests");
var samplesContainerOld = cosmosClient.GetContainer("APIView", "UsageSamples");

var reviewsContainerNew = cosmosClient.GetContainer("APIViewV2", "Reviews");
var revisionsContainerNew = cosmosClient.GetContainer("APIViewV2", "APIRevisions");
var commentsContainerNew = cosmosClient.GetContainer("APIViewV2", "Comments");
var prContainerNew = cosmosClient.GetContainer("APIViewV2", "PullRequests");
var samplesContainerNew = cosmosClient.GetContainer("APIViewV2", "SamplesRevisions");
var mappingsContainer = cosmosClient.GetContainer("APIViewV2", "LegacyMappings");

static string ArrayToQueryString<T>(IEnumerable<T> items)
{
    var result = new StringBuilder();
    result.Append("(");
    foreach (var item in items)
    {
        if (item is int)
        {
            result.Append($"{item},");
        }
        else
        {
            result.Append($"\"{item}\",");
        }

    }
    if (result[result.Length - 1] == ',')
    {
        result.Remove(result.Length - 1, 1);
    }
    result.Append(")");
    return result.ToString();
}

static async Task MigrateDocuments(
    Container reviewsContainerOld, Container reviewsContainerNew,
    Container prContainerOld, Container prContainerNew,
    Container samplesContainerOld, Container samplesContainerNew,
    Container commentsContainerOld, Container commentsContainerNew,
    Container revisionsContainerNew, Container mappingsContainer, int? limit = null)
{
    var reviewsOld = new List<ReviewModelOld>();
    var reviewsOldQuery = $"SELECT * FROM Reviews c Where c.IsClosed != true AND Exists(Select Value r from r in c.Revisions where IS_DEFINED(r.Files[0].PackageName) AND r.Files[0].PackageName != \"\" AND NOT IS_NULL(r.Files[0].PackageName))";
    var reviewsOldQueryDefinition = new QueryDefinition(reviewsOldQuery);
    var reviewsOldItemQueryIterator = reviewsContainerOld.GetItemQueryIterator<ReviewModelOld>(reviewsOldQueryDefinition);

    while (reviewsOldItemQueryIterator.HasMoreResults)
    {
        var response = await reviewsOldItemQueryIterator.ReadNextAsync();
        reviewsOld.AddRange(response.Resource);
    }

    int i = 0;
    int totalReviews = reviewsOld.Count;

    foreach (var reviewOld in reviewsOld)
    {
        i++;
        Console.WriteLine($"Status: Migrating {i} of {totalReviews} reviews.");
        if (limit.HasValue)
        {
            if (limit == 0)
            {
                break;
            }
            limit--;
        }

        var revisionWithPackageName = reviewOld.Revisions.LastOrDefault(r => !String.IsNullOrEmpty(r.Files[0].PackageName));
        var revisonWithlanguage = reviewOld.Revisions.LastOrDefault(r => !String.IsNullOrEmpty(r.Files[0].Language));

        if (revisionWithPackageName == null || revisonWithlanguage == null)
        {
            Console.WriteLine($"Package name or language is empty for review {reviewOld.ReviewId}");
            continue;
        }

        var packageName = revisionWithPackageName.Files[0].PackageName;
        var language = revisonWithlanguage.Files[0].Language;

        if (language == "C" || language == "C++")
        {
            packageName = packageName.Replace("::", "_").ToLower();
        }

        var reviewNew = default(ReviewModel);
        var mapping = default(MappingModel);

        // Get existing review if it exist otherwise create new review
        var reviewNewQuery = $"SELECT * FROM Reviews c Where c.PackageName = \"{packageName}\" AND c.Language = \"{language}\"";
        var reviewNewQueryDefinition = new QueryDefinition(reviewNewQuery);
        var reviewNewItemQueryIterator = reviewsContainerNew.GetItemQueryIterator<ReviewModel>(reviewNewQueryDefinition);

        while (reviewNewItemQueryIterator.HasMoreResults)
        {
            var response = await reviewNewItemQueryIterator.ReadNextAsync();
            reviewNew = response.Resource.FirstOrDefault();
        }

        if (reviewNew == null)
        {
            // Create new Review
            reviewNew = new ReviewModel()
            {
                Id = Guid.NewGuid().ToString("N"),
                PackageName = packageName,
                Language = language,
                IsClosed = true,
            };

            // Create mapping
            mapping = new MappingModel()
            {
                ReviewNewId = reviewNew.Id,
                ReviewOldIds = new HashSet<string>() { reviewOld.ReviewId }
            };
        }
        else
        {
            // Get mapping
            mapping = await mappingsContainer.ReadItemAsync<MappingModel>(reviewNew.Id, new PartitionKey(reviewNew.Id));
            if (mapping == null)
            {
                mapping = new MappingModel()
                {
                    ReviewNewId = reviewNew.Id,
                    ReviewOldIds = new HashSet<string>()
                }; 
            }              
            mapping.ReviewOldIds.Add(reviewOld.ReviewId);
        }

        if (reviewOld.IsApprovedForFirstRelease)
        {
            reviewNew.IsApproved = true;
            reviewNew.ChangeHistory.Add(new ReviewChangeHistoryModel()
                {
                    ChangeAction = ReviewChangeAction.Approved,
                    ChangedBy = reviewOld.ApprovedForFirstReleaseBy,
                    ChangedOn = reviewOld.ApprovedForFirstReleaseOn
                });
        }
        // Create APIRevisions
        foreach (var revisionOld in reviewOld.Revisions)
        {
            if (language != "Swagger" && language != "TypeSpec" && reviewOld.Revisions.Count > 1 && reviewOld.FilterType == APIRevisionType.PullRequest && revisionOld.RevisionNumber == 0)
            {
                // Skip Baseline of PR Revision which is a duplicate of Automatic;
                continue;
            }

            // Copuy RevisionOld to RevisionNew
            var apiRevisionNew = new APIRevisionModel();
            apiRevisionNew.Id = revisionOld.RevisionId;
            apiRevisionNew.ReviewId = reviewNew.Id;
            apiRevisionNew.PackageName = reviewNew.PackageName;
            apiRevisionNew.Language = reviewNew.Language;

            foreach (var file in revisionOld.Files)
            {
                apiRevisionNew.Files.Add(
                    new APICodeFileModel()
                    {
                        FileId = file.ReviewFileId,
                        Name = file.Name,
                        Language = reviewNew.Language,
                        VersionString = file.VersionString,
                        LanguageVariant = file.LanguageVariant,
                        HasOriginal = file.HasOriginal,
                        CreationDate = file.CreationDate,
                        RunAnalysis = file.RunAnalysis,
                        PackageName = reviewNew.PackageName,
                        FileName = file.FileName,
                        PackageVersion = file.PackageVersion
                    }
                );
            }

            apiRevisionNew.Label = revisionOld.Label;
            apiRevisionNew.ChangeHistory.Add(new APIRevisionChangeHistoryModel()
            {
                ChangeAction = APIRevisionChangeAction.Created,
                ChangedBy = revisionOld.Author,
                ChangedOn = revisionOld.CreationDate
            });
            apiRevisionNew.CreatedBy = revisionOld.Author;
            apiRevisionNew.CreatedOn = revisionOld.CreationDate;

            // Open review once a revision is added
            reviewNew.IsClosed = false;

            if (reviewNew.ChangeHistory.Any() && reviewNew.ChangeHistory.Where(ch => ch.ChangeAction == ReviewChangeAction.Created).Any())
            {
                if (
                    reviewNew.ChangeHistory.First(ch => ch.ChangeAction == ReviewChangeAction.Created).ChangedOn == default(DateTime) ||
                    reviewNew.ChangeHistory.First(ch => ch.ChangeAction == ReviewChangeAction.Created).ChangedOn > revisionOld.CreationDate)
                {
                    reviewNew.ChangeHistory.First(ch => ch.ChangeAction == ReviewChangeAction.Created).ChangedOn = revisionOld.CreationDate;
                    reviewNew.ChangeHistory.First(ch => ch.ChangeAction == ReviewChangeAction.Created).ChangedBy = revisionOld.Author;
                    reviewNew.CreatedOn = revisionOld.CreationDate;
                    reviewNew.CreatedBy = revisionOld.Author;
                }
            }
            else
            {
                reviewNew.ChangeHistory.Add(new ReviewChangeHistoryModel()
                {
                    ChangeAction = ReviewChangeAction.Created,
                    ChangedBy = revisionOld.Author,
                    ChangedOn = revisionOld.CreationDate
                });
                reviewNew.CreatedOn = revisionOld.CreationDate;
                reviewNew.CreatedBy = revisionOld.Author;
            }

            if (reviewNew.LastUpdatedOn == default(DateTime) || reviewNew.LastUpdatedOn < apiRevisionNew.CreatedOn)
            {
                // Update last updated on date if its before 
                reviewNew.LastUpdatedOn = apiRevisionNew.CreatedOn;
            }

            // Update Approvals
            if (revisionOld.IsApproved)
            {
                reviewNew.IsApproved = true;
                apiRevisionNew.IsApproved = true;
                foreach (var approver in revisionOld.Approvers)
                {
                    apiRevisionNew.ChangeHistory.Add(new APIRevisionChangeHistoryModel()
                    {
                        ChangeAction = APIRevisionChangeAction.Approved,
                        ChangedBy = approver
                    });
                    apiRevisionNew.Approvers.Add(approver);
                }
            }

            apiRevisionNew.APIRevisionType = reviewOld.FilterType;
            if (revisionOld.HeadingsOfSectionsWithDiff.Where(items => items.Value.Any()).Any())
            {
                foreach (var key in revisionOld.HeadingsOfSectionsWithDiff.Keys)
                {
                    if (revisionOld.HeadingsOfSectionsWithDiff[key].Any())
                    {
                        apiRevisionNew.HeadingsOfSectionsWithDiff.Add(key, revisionOld.HeadingsOfSectionsWithDiff[key]);
                    }
                }
            }
            apiRevisionNew.IsDeleted = false;

            Console.WriteLine($"Creating APIRevision: {apiRevisionNew.Id}");
            await revisionsContainerNew.UpsertItemAsync(apiRevisionNew, new PartitionKey(apiRevisionNew.ReviewId));

            // Create Comments Associated with this RevisionOld
            var commentsOld = new List<CommentModelOld>();
            var commentsOldQuery = $"SELECT * FROM c WHERE c.RevisionId = @revisionId";
            var commentsQueryDefinition = new QueryDefinition(commentsOldQuery).WithParameter("@revisionId", revisionOld.RevisionId);
            var itemQueryIterator = commentsContainerOld.GetItemQueryIterator<CommentModelOld>(commentsQueryDefinition);

            while (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                commentsOld.AddRange(result.Resource);
            }

            foreach (var comment in commentsOld)
            {
                var commentNew = new CommentModel();
                commentNew.Id = comment.CommentId;
                commentNew.ReviewId = reviewNew.Id;
                commentNew.APIRevisionId = apiRevisionNew.Id;
                commentNew.ElementId = comment.ElementId;
                commentNew.SectionClass = comment.SectionClass;
                commentNew.CommentText = comment.Comment;
                commentNew.ChangeHistory.Add(new CommentChangeHistoryModel()
                {
                    ChangeAction = CommentChangeAction.Created,
                    ChangedBy = comment.Username,
                    ChangedOn = comment.TimeStamp
                });
                commentNew.CreatedOn = comment.TimeStamp;
                commentNew.CreatedBy = comment.Username;
                if (comment.EditedTimeStamp != null)
                {
                    commentNew.ChangeHistory.Add(new CommentChangeHistoryModel()
                    {
                        ChangeAction = CommentChangeAction.Edited,
                        ChangedBy = comment.Username,
                        ChangedOn = comment.EditedTimeStamp
                    });
                }
                commentNew.LastEditedOn = comment.EditedTimeStamp;
                commentNew.IsResolved = comment.IsResolve;
                commentNew.Upvotes = comment.Upvotes;
                commentNew.TaggedUsers = comment.TaggedUsers;
                commentNew.CommentType = (comment.IsUsageSampleComment) ? CommentType.SampleRevision : CommentType.APIRevision;
                commentNew.ResolutionLocked = comment.ResolutionLocked;
                commentNew.IsDeleted = false;

                Console.WriteLine($"Creating New Comment {commentNew.Id} with ReviewId {commentNew.ReviewId}");
                await commentsContainerNew.UpsertItemAsync(commentNew, new PartitionKey(commentNew.ReviewId));
            }
        }

        // Create Pull Requests Associated with the ReviewOld
        var pullRequestsOld = new List<PullRequestModelOld>();
        var prQuery = $"SELECT * FROM c WHERE c.ReviewId = @reviewId";
        var prQueryDefinition = new QueryDefinition(prQuery).WithParameter("@reviewId", reviewOld.ReviewId);
        var prItemQueryIterator = prContainerOld.GetItemQueryIterator<PullRequestModelOld>(prQueryDefinition);
        while (prItemQueryIterator.HasMoreResults)
        {
            var result = await prItemQueryIterator.ReadNextAsync();
            pullRequestsOld.AddRange(result.Resource);
        }

        foreach (var prModelOld in pullRequestsOld)
        {
            var prModelNew = new PullRequestModel();
            prModelNew.Id = prModelOld.PullRequestId;
            prModelNew.PullRequestNumber = prModelOld.PullRequestNumber;
            prModelNew.Commits = prModelOld.Commits;
            prModelNew.RepoName = prModelOld.RepoName;
            prModelNew.FilePath = prModelOld.FilePath;
            prModelNew.IsOpen = prModelOld.IsOpen;
            prModelNew.ReviewId = reviewNew.Id;
            prModelNew.CreatedBy = prModelOld.Author;
            prModelNew.PackageName = reviewNew.PackageName;
            prModelNew.Language = reviewNew.Language;
            prModelNew.Assignee = prModelOld.Assignee;
            prModelNew.IsDeleted = false;

            var oldReview = reviewsOld.Where(rev => rev.ReviewId == prModelOld.ReviewId)?.FirstOrDefault();
            if (oldReview != null)
            {
                prModelNew.APIRevisionId = oldReview.Revisions.Last()?.RevisionId;
                Console.WriteLine($"Setting previous revision ID {prModelNew.APIRevisionId} from review {oldReview.ReviewId} as API revision ID for PR Model {prModelNew.Id}");
            }
            Console.WriteLine($"Creating PR    : {prModelNew.Id}");
            await prContainerNew.UpsertItemAsync(prModelNew, new PartitionKey(prModelNew.ReviewId));
        }

        // Create Sample Revisions Associated with the Review
        var samplesOld = new List<UsageSampleModel>();
        var samplesQuery = $"SELECT * FROM c WHERE c.ReviewId = @reviewId";
        var samplesQueryDefinition = new QueryDefinition(samplesQuery).WithParameter("@reviewId", reviewOld.ReviewId);
        var samplesItemQueryIterator = samplesContainerOld.GetItemQueryIterator<UsageSampleModel>(samplesQueryDefinition);
        while (samplesItemQueryIterator.HasMoreResults)
        {
            var result = await samplesItemQueryIterator.ReadNextAsync();
            samplesOld.AddRange(result.Resource);
        }

        foreach (var sampleOld in samplesOld)
        {
            foreach (var sampleOldRevision in sampleOld.Revisions)
            {
                var sampleNewRevision = new SampleRevisionModel();
                sampleNewRevision.Id = Guid.NewGuid().ToString("N");
                sampleNewRevision.ReviewId = reviewNew.Id;
                sampleNewRevision.PackageName = reviewNew.PackageName;
                sampleNewRevision.Language = reviewNew.Language;
                sampleNewRevision.FileId = sampleOldRevision.FileId;
                sampleNewRevision.OriginalFileId = sampleOldRevision.OriginalFileId;
                sampleNewRevision.OriginalFileName = sampleOldRevision.OriginalFileName;
                sampleNewRevision.CreatedBy = sampleOldRevision.CreatedBy;
                sampleNewRevision.CreatedOn = sampleOldRevision.CreatedOn;
                sampleNewRevision.Title = sampleOldRevision.RevisionTitle;
                sampleNewRevision.IsDeleted = sampleOldRevision.RevisionIsDeleted;

                Console.WriteLine($"Creating Sample: {sampleNewRevision.Id}");
                await samplesContainerNew.UpsertItemAsync(sampleNewRevision, new PartitionKey(sampleNewRevision.ReviewId));
            }
        }

        // Create Comments For cases where RevisionId is null or not defined 
        var commentsOld2 = new List<CommentModelOld>();
        var commentsOldQuery2 = $"SELECT * FROM c WHERE c.ReviewId = @reviewId AND (c.RevisionId = null OR NOT IS_DEFINED(c.RevisionId))";
        var commentsQueryDefinition2 = new QueryDefinition(commentsOldQuery2).WithParameter("@reviewId", reviewOld.ReviewId);
        var itemQueryIterator2 = commentsContainerOld.GetItemQueryIterator<CommentModelOld>(commentsQueryDefinition2);

        while (itemQueryIterator2.HasMoreResults)
        {
            var result = await itemQueryIterator2.ReadNextAsync();
            commentsOld2.AddRange(result.Resource);
        }

        foreach (var comment in commentsOld2)
        {
            var commentNew = new CommentModel();
            commentNew.Id = comment.CommentId;
            commentNew.ReviewId = reviewNew.Id;
            commentNew.ElementId = comment.ElementId;
            commentNew.SectionClass = comment.SectionClass;
            commentNew.CommentText = comment.Comment;
            commentNew.ChangeHistory.Add(new CommentChangeHistoryModel()
            {
                ChangeAction = CommentChangeAction.Created,
                ChangedBy = comment.Username,
                ChangedOn = comment.TimeStamp
            });
            commentNew.CreatedOn = comment.TimeStamp;
            commentNew.CreatedBy = comment.Username;
            if (comment.EditedTimeStamp != null)
            {
                commentNew.ChangeHistory.Add(new CommentChangeHistoryModel()
                {
                    ChangeAction = CommentChangeAction.Edited,
                    ChangedBy = comment.Username,
                    ChangedOn = comment.EditedTimeStamp
                });
            }
            commentNew.LastEditedOn = comment.EditedTimeStamp;
            commentNew.IsResolved = comment.IsResolve;
            commentNew.Upvotes = comment.Upvotes;
            commentNew.TaggedUsers = comment.TaggedUsers;
            commentNew.CommentType = (comment.IsUsageSampleComment) ? CommentType.SampleRevision : CommentType.APIRevision;
            commentNew.ResolutionLocked = comment.ResolutionLocked;
            commentNew.IsDeleted = false;

            Console.WriteLine($"Creating New Comment {commentNew.Id} with ReviewId {commentNew.ReviewId}");
            await commentsContainerNew.UpsertItemAsync(commentNew, new PartitionKey(commentNew.ReviewId));
        }

        // Update review
        Console.WriteLine($"Update Review: {reviewNew.Id}");
        await reviewsContainerNew.UpsertItemAsync(reviewNew, new PartitionKey(reviewNew.Id));

        // Update mappings
        Console.WriteLine($"Update Mapping: {mapping.ReviewNewId}");
        await mappingsContainer.UpsertItemAsync(mapping, new PartitionKey(mapping.ReviewNewId));
    }

}
await MigrateDocuments(
    reviewsContainerOld: reviewsContainerOld, reviewsContainerNew: reviewsContainerNew,
    prContainerOld: prContainerOld, prContainerNew: prContainerNew,
    samplesContainerOld: samplesContainerOld, samplesContainerNew: samplesContainerNew,
    commentsContainerOld: commentsContainerOld, commentsContainerNew: commentsContainerNew,
    revisionsContainerNew: revisionsContainerNew, mappingsContainer: mappingsContainer);










