using System.Threading.Tasks;
using Xunit;
using System.IO;
using System;
using APIViewWeb;
using APIViewWeb.Repositories;
using Newtonsoft.Json;
using APIViewWeb.Models;
using System.Collections.Generic;
using ApiView;
using FluentAssertions;

namespace APIViewIntegrationTests
{
    [Collection("TestsBase Collection")]
    public class ReviewManagerTests : IDisposable
    {
        TestsBaseFixture testsBaseFixture;
        FileStream fileStreamA;
        FileStream fileStreamB;
        FileStream fileStreamC;
        FileStream fileStreamD;

        string fileNameA = "TokenFileWithSectionsRevision1.json";
        string fileNameB = "TokenFileWithSectionsRevision2.json";
        string fileNameC = "Azure.Analytics.Purview.AccountRev1.json";
        string fileNameD = "Azure.Analytics.Purview.AccountRev2.json";

        public ReviewManagerTests(TestsBaseFixture testsBaseFixture)
        {
            this.testsBaseFixture = testsBaseFixture;
            string filePathA = Path.Combine("SampleTestFiles", fileNameA);
            string filePathB = Path.Combine("SampleTestFiles", fileNameB);
            string filePathC = Path.Combine("SampleTestFiles", fileNameC);
            string filePathD = Path.Combine("SampleTestFiles", fileNameD);

            FileInfo fileInfoA = new FileInfo(filePathA);
            FileInfo fileInfoB = new FileInfo(filePathB);
            FileInfo fileInfoC = new FileInfo(filePathC);
            FileInfo fileInfoD = new FileInfo(filePathD);

            fileStreamA = fileInfoA.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStreamB = fileInfoB.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStreamC = fileInfoC.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStreamD = fileInfoD.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Dispose()
        {
            fileStreamA.DisposeAsync();
            fileStreamB.DisposeAsync();
            fileStreamC.DisposeAsync();
            fileStreamD.DisposeAsync();
        }

        [Fact]
        public async Task AddRevisionAsync_Computes_Headings_Of_Sections_With_Diff_A()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var user = testsBaseFixture.User;
            var review = await testsBaseFixture.ReviewManager.CreateReviewAsync(user, fileNameA, "Revision1", fileStreamA, false, "Swagger", true);
            await reviewManager.AddRevisionAsync(user, review.ReviewId, fileNameB, "Revision2", fileStreamB, "Swagger", true);
            review = await reviewManager.GetReviewAsync(user, review.ReviewId);
            var headingWithDiffInSections = review.Revisions[0].HeadingsOfSectionsWithDiff[review.Revisions[1].RevisionId];
            Assert.All(headingWithDiffInSections,
                item => Assert.Contains(item, new int[] { 2, 16 }));
        }

        [Fact]
        public async Task AddRevisionAsync_Computes_Headings_Of_Sections_With_Diff_B()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var user = testsBaseFixture.User;
            var review = await reviewManager.CreateReviewAsync(user, fileNameC, "Azure.Analytics.Purview.Account", fileStreamC, false, "Swagger", true);
            await reviewManager.AddRevisionAsync(user, review.ReviewId, fileNameD, "Azure.Analytics.Purview.Account", fileStreamD, "Swagger", true);
            review = await reviewManager.GetReviewAsync(user, review.ReviewId);
            var headingWithDiffInSections = review.Revisions[0].HeadingsOfSectionsWithDiff[review.Revisions[1].RevisionId];
            Assert.All(headingWithDiffInSections,
                item => Assert.Contains(item, new int[] { 20, 275 }));
        }

        [Fact]
        public async Task Delete_PullRequest_Review_Throws_Exception()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var user = testsBaseFixture.User;
            var review = await reviewManager.CreateReviewAsync(user, fileNameC, "Azure.Analytics.Purview.Account", fileStreamC, false, "Swagger", false);
            Assert.Equal(ReviewType.Manual, review.FilterType);

            review.FilterType = ReviewType.PullRequest;

            // Change review type to PullRequest
            await testsBaseFixture.ReviewRepository.UpsertReviewAsync(review);
            Assert.Equal(ReviewType.PullRequest, review.FilterType);

            await Assert.ThrowsAsync<UnDeletableReviewException>(async () => await reviewManager.DeleteRevisionAsync(user, review.ReviewId, review.Revisions[0].RevisionId));
        }

        [Fact(Skip = "Need Resource to run so won't run on PR piplines plus Only needed once.")]
        public async Task UpdateSwaggerReviewsMetaData_Test()
        {
            string reviewJson = File.ReadAllText(Path.Join(testsBaseFixture.TestDataPath, "account.swagger-cosmos-data.json"));
            ReviewModel testReview = JsonConvert.DeserializeObject<ReviewModel>(reviewJson);
            await testsBaseFixture.ReviewRepository.UpsertReviewAsync(testReview);

            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Join(testsBaseFixture.TestDataPath, "testComments"));

            foreach(var file in directoryInfo.GetFiles())
            {
                string commentJson = File.ReadAllText(file.FullName);
                CommentModel comment = JsonConvert.DeserializeObject<CommentModel>(commentJson);
                await testsBaseFixture.CommentRepository.UpsertCommentAsync(comment);
            }

            foreach (var revision in testReview.Revisions)
            {
                string codeFileJson = File.ReadAllText(Path.Join(testsBaseFixture.TestDataPath, "codeFiles", revision.Files[0].ReviewFileId));
                CodeFile testCodeFile = JsonConvert.DeserializeObject<CodeFile>(codeFileJson);
                await testsBaseFixture.BlobCodeFileRepository.UpsertCodeFileAsync(revision.RevisionId, revision.Files[0].ReviewFileId, testCodeFile);
            } 

            await testsBaseFixture.ReviewManager.UpdateSwaggerReviewsMetaData();

            ReviewModel updatedReview = await testsBaseFixture.ReviewRepository.GetReviewAsync(testReview.ReviewId);
            IEnumerable<CommentModel> updatedComments = await testsBaseFixture.CommentRepository.GetCommentsAsync(testReview.ReviewId);

            List<string> expectedCommentIds = new List<string>() {
                "-account.swagger-General-consumes",
                "-account.swagger-Paths-/",
                "-account.swagger-Paths-/collections-0-operationId-Collections_ListCollections-QueryParameters-table-tr-2",
                "-account.swagger-Paths-/collections-0-operationId-Collections_ListCollections-Responses-200-table-tr-3"
            };

            foreach (var comment in updatedComments)
            {
                expectedCommentIds.Should().Contain(comment.ElementId);
            }

            int[] expectedLineNumbers = { 1, 2, 3, 7, 8, 9, 10, 11, 20, 26, 860, 1184, 1193};
            var renderedCodeFile = await testsBaseFixture.BlobCodeFileRepository.GetCodeFileAsync(updatedReview.Revisions[1]);
            renderedCodeFile.Render(false);

            for (int i = 0; i < renderedCodeFile.RenderResult.CodeLines.Length; i++)
            {
                Assert.Equal(renderedCodeFile.RenderResult.CodeLines[i].LineNumber, expectedLineNumbers[i]);
            }
        }
    }
}
