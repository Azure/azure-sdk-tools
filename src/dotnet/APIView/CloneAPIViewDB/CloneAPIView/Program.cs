using CloneAPIViewDB;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
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
var mappingsContainer = cosmosClient.GetContainer("APIViewV2", "MigrationStatus");


static async Task MigrateDocuments(
    Container reviewsContainerOld, Container reviewsContainerNew,
    Container prContainerOld, Container prContainerNew,
    Container samplesContainerOld, Container samplesContainerNew,
    Container commentsContainerOld, Container commentsContainerNew,
    Container revisionsContainerNew, Container mappingsContainer, int? limit = null)
{
    Console.WriteLine($"Migration starts at {DateTime.Now}");

    var reviewsOld = new List<ReviewModelOld>();
    var reviewsOldQuery = $"SELECT * FROM Reviews c Where c.IsClosed != true AND Exists(Select Value r from r in c.Revisions where IS_DEFINED(r.Files[0].PackageName) AND r.Files[0].PackageName != \"\" AND NOT IS_NULL(r.Files[0].PackageName))";
    var reviewsOldQueryDefinition = new QueryDefinition(reviewsOldQuery);
    var reviewsOldItemQueryIterator = reviewsContainerOld.GetItemQueryIterator<ReviewModelOld>(reviewsOldQueryDefinition);
    while (reviewsOldItemQueryIterator.HasMoreResults)
    {
        var response = await reviewsOldItemQueryIterator.ReadNextAsync();
        reviewsOld.AddRange(response.Resource);
    }

    var mappings = new List<MappingModel>();
    var mappingQueryDef = new QueryDefinition("SELECT * FROM MigrationStatus m");
    var mappingItemQueryIterator = mappingsContainer.GetItemQueryIterator<MappingModel>(mappingQueryDef);
    while(mappingItemQueryIterator.HasMoreResults)
    {
        var response = await mappingItemQueryIterator.ReadNextAsync();
        mappings.AddRange(response.Resource);
    }
    
    int i = 0;
    int totalReviews = reviewsOld.Count;

    foreach (var reviewOld in reviewsOld)
    {
        i++;
        Console.WriteLine($"Status: Migrating {i} of {totalReviews} reviews.");       

        var mapping = mappings.FirstOrDefault(m => m.ReviewOldId == reviewOld.ReviewId);
        if (mapping == null)
        {
            mapping = new MappingModel()
            {
                ReviewOldId = reviewOld.ReviewId
            };
            mappings.Add(mapping);
        }

        if(reviewOld._ts <= mapping.ReviewMigratedStamp)
        {
            Console.WriteLine($"Review {reviewOld.ReviewId} was already migrated. Skipping it.");
            continue;
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
        }

        mapping.ReviewNewId = reviewNew.Id;
        mapping.PackageName = packageName;
        mapping.Language = language;

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
            await MigrateRevision(revisionOld, reviewOld, language, packageName, reviewNew, revisionsContainerNew);
        }                

        if (reviewNew.IsClosed)
        {
            Console.WriteLine($"Skipping review {reviewOld.ReviewId} without any revisions");
            continue;
        }            

        // Update review
        Console.WriteLine($"Update Review: {reviewNew.Id}");
        await reviewsContainerNew.UpsertItemAsync(reviewNew, new PartitionKey(reviewNew.Id));

        // Update mappings
        Console.WriteLine($"Update Mapping: {mapping.ReviewOldId}");
        mapping.ReviewMigratedStamp = reviewOld._ts;
        await mappingsContainer.UpsertItemAsync(mapping, new PartitionKey(mapping.ReviewOldId));

        if (limit.HasValue)
        {
            limit--;
            if (limit <= 0)
            {
                break;
            }            
        }
    }

    Console.WriteLine("Migrating Comments");
    await MigrateComments(commentsContainerOld, commentsContainerNew, mappings, reviewsOld, mappingsContainer);

    Console.WriteLine("Migrating pull request model");
    await MigratePullRequestModels(prContainerOld, prContainerNew, mappings, reviewsOld, mappingsContainer);

    Console.WriteLine("Migrating samples");
    await MigrateSamples(samplesContainerOld, samplesContainerNew, mappings, mappingsContainer);

    Console.WriteLine($"Migration Ends at {DateTime.Now}");
}

static async Task MigrateRevision(RevisionModelOld revisionOld, 
    ReviewModelOld reviewOld, 
    string language, 
    string packageName, 
    ReviewModel reviewNew,
    Container revisionsContainerNew
    )
{
    if (language != "Swagger" && language != "TypeSpec" && reviewOld.Revisions.Count > 1 && reviewOld.FilterType == APIRevisionType.PullRequest && revisionOld.RevisionNumber == 0)
    {
        // Skip Baseline of PR Revision which is a duplicate of Automatic;
        return;
    }

    if(reviewOld.FilterType == APIRevisionType.Manual && revisionOld.IsApproved !=  true && revisionOld.CreationDate.AddYears(2) > DateTime.UtcNow)
    {
        Console.WriteLine($"Skipping older manual revision for review {reviewOld.ReviewId}");
        return;
    }
    // Copuy RevisionOld to RevisionNew
    var apiRevisionNew = new APIRevisionModel();
    apiRevisionNew.Id = revisionOld.RevisionId;
    apiRevisionNew.ReviewId = reviewNew.Id;
    apiRevisionNew.PackageName = reviewNew.PackageName;
    apiRevisionNew.Language = reviewNew.Language;

    foreach (var file in revisionOld.Files)
    {
        apiRevisionNew.Files.Add(new APICodeFileModel(file, reviewNew.Language, reviewNew.PackageName));
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

    //Console.WriteLine($"Creating APIRevision: {apiRevisionNew.Id}");
    await revisionsContainerNew.UpsertItemAsync(apiRevisionNew, new PartitionKey(apiRevisionNew.ReviewId));
}

static async Task MigratePullRequestModels(Container prContainerOld, Container prContainerNew, List<MappingModel> reviewMap, List<ReviewModelOld> reviewsOld, Container mappingContainer)
{
    foreach (var mapping in reviewMap)
    {
        var reviewOld = reviewsOld.FirstOrDefault(r => r.ReviewId == mapping.ReviewOldId);
        if (reviewOld == null || reviewOld.FilterType != APIRevisionType.PullRequest)
            continue;

        // Create Pull Requests Associated with the ReviewOld
        var pullRequestsOld = new List<PullRequestModelOld>();
        var prQuery = $"SELECT * FROM c where c.ReviewId = @reviewId and c.IsClosed != true";
        var prQueryDefinition = new QueryDefinition(prQuery).WithParameter("@reviewId", reviewOld.ReviewId);
        var prItemQueryIterator = prContainerOld.GetItemQueryIterator<PullRequestModelOld>(prQueryDefinition);
        while (prItemQueryIterator.HasMoreResults)
        {
            var result = await prItemQueryIterator.ReadNextAsync();
            pullRequestsOld.AddRange(result.Resource);
        }
                
        foreach (var prModelOld in pullRequestsOld)
        {
            Console.WriteLine($"Migrating pull request {prModelOld.ReviewId}");
            if (prModelOld._ts <= mapping.PullRequestMigratedStamp)
            {
                Console.WriteLine($"Pull request ID {prModelOld.ReviewId} was already processed.");
                continue;
            }

            var prModelNew = new PullRequestModel();
            prModelNew.Id = prModelOld.PullRequestId;
            prModelNew.PullRequestNumber = prModelOld.PullRequestNumber;
            prModelNew.Commits = prModelOld.Commits;
            prModelNew.RepoName = prModelOld.RepoName;
            prModelNew.FilePath = prModelOld.FilePath;
            prModelNew.IsOpen = prModelOld.IsOpen;
            prModelNew.ReviewId = mapping.ReviewNewId;
            prModelNew.CreatedBy = prModelOld.Author;
            prModelNew.PackageName = mapping.PackageName;
            prModelNew.Language = mapping.Language;
            prModelNew.Assignee = prModelOld.Assignee;
            prModelNew.IsDeleted = false;

            var oldReview = reviewsOld.Where(rev => rev.ReviewId == prModelOld.ReviewId)?.FirstOrDefault();
            if (oldReview != null)
            {
                prModelNew.APIRevisionId = oldReview.Revisions.Last().RevisionId;
                Console.WriteLine($"Setting previous revision ID {prModelNew.APIRevisionId} from review {oldReview.ReviewId} as API revision ID for PR Model {prModelNew.Id}");
            }
            Console.WriteLine($"Creating PR    : {prModelNew.Id}");
            await prContainerNew.UpsertItemAsync(prModelNew, new PartitionKey(prModelNew.ReviewId));
            mapping.PullRequestMigratedStamp = prModelOld._ts;
            await mappingContainer.UpsertItemAsync(mapping);
        }
    }    
}


static async Task MigrateSamples(Container samplesContainerOld, Container samplesContainerNew,  List<MappingModel> reviewMap, Container mappingContainer)
{
    foreach (var mapping in reviewMap)
    {
        // Create Sample Revisions Associated with the Review
        var samplesOld = new List<UsageSampleModel>();
        var samplesQuery = $"SELECT * FROM c where c.ReviewId = @reviewId";
        var samplesQueryDefinition = new QueryDefinition(samplesQuery).WithParameter("@reviewId", mapping.ReviewOldId);
        var samplesItemQueryIterator = samplesContainerOld.GetItemQueryIterator<UsageSampleModel>(samplesQueryDefinition);
        while (samplesItemQueryIterator.HasMoreResults)
        {
            var result = await samplesItemQueryIterator.ReadNextAsync();
            samplesOld.AddRange(result.Resource);
        }

        foreach (var sampleOld in samplesOld)
        {
            if (sampleOld._ts <= mapping.SampleMigratedStamp)
            {
                Console.WriteLine($"Samples for {sampleOld.ReviewId} was already processed.");
                continue;
            }

            Console.WriteLine($"Migrating sample for review {sampleOld.ReviewId}");
            foreach (var sampleOldRevision in sampleOld.Revisions)
            {
                var sampleNewRevision = new SampleRevisionModel();
                sampleNewRevision.Id = Guid.NewGuid().ToString("N");
                sampleNewRevision.ReviewId = mapping.ReviewNewId;
                sampleNewRevision.PackageName = mapping.PackageName;
                sampleNewRevision.Language = mapping.Language;
                sampleNewRevision.FileId = sampleOldRevision.FileId;
                sampleNewRevision.OriginalFileId = sampleOldRevision.OriginalFileId;
                sampleNewRevision.OriginalFileName = sampleOldRevision.OriginalFileName;
                sampleNewRevision.CreatedBy = sampleOldRevision.CreatedBy;
                sampleNewRevision.CreatedOn = sampleOldRevision.CreatedOn;
                sampleNewRevision.Title = sampleOldRevision.RevisionTitle;
                sampleNewRevision.IsDeleted = sampleOldRevision.RevisionIsDeleted;

                await samplesContainerNew.UpsertItemAsync(sampleNewRevision, new PartitionKey(sampleNewRevision.ReviewId));
            }
            mapping.SampleMigratedStamp = sampleOld._ts;
            await mappingContainer.UpsertItemAsync(mapping);
        }
    }
}

static async Task MigrateComments(Container commentsContainerOld, Container commentsContainerNew, List<MappingModel> reviewMap, List<ReviewModelOld> reviewsOld, Container mappingContainer)
{
    foreach(var mapping in reviewMap)
    {
        var reviewOld = reviewsOld.FirstOrDefault( r => r.ReviewId == mapping.ReviewOldId );
        if (reviewOld == null)
            continue;

        // Create Comments Associated with this RevisionOld
        var commentsOld = new List<CommentModelOld>();
        var commentsOldQuery = $"SELECT * FROM c  where c.ReviewId = @reviewId order by c._ts";
        var commentsQueryDefinition = new QueryDefinition(commentsOldQuery).WithParameter("@reviewId", reviewOld.ReviewId);
        var itemQueryIterator = commentsContainerOld.GetItemQueryIterator<CommentModelOld>(commentsQueryDefinition);

        while (itemQueryIterator.HasMoreResults)
        {
            var result = await itemQueryIterator.ReadNextAsync();
            commentsOld.AddRange(result.Resource);
        }

        foreach (var comment in commentsOld)
        {
            if (comment._ts <= mapping.CommentMigratedStamp)
            {
                continue;
            }

            var commentNew = new CommentModel();
            commentNew.Id = comment.CommentId;
            commentNew.ReviewId = mapping.ReviewNewId;
            commentNew.APIRevisionId = comment.RevisionId;
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
            mapping.CommentMigratedStamp = comment._ts;
            await commentsContainerNew.UpsertItemAsync(commentNew, new PartitionKey(commentNew.ReviewId));
        }
        await mappingContainer.UpsertItemAsync(mapping);
    }
}

await MigrateDocuments(
    reviewsContainerOld: reviewsContainerOld, reviewsContainerNew: reviewsContainerNew,
    prContainerOld: prContainerOld, prContainerNew: prContainerNew,
    samplesContainerOld: samplesContainerOld, samplesContainerNew: samplesContainerNew,
    commentsContainerOld: commentsContainerOld, commentsContainerNew: commentsContainerNew,
    revisionsContainerNew: revisionsContainerNew, mappingsContainer: mappingsContainer);










