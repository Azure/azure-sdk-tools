using System.Threading.Tasks;
using Xunit;
using System.IO;
using System;
using APIViewWeb;
using APIViewWeb.Repositories;

#if false
// Disabling test.
// New tests need to be added to accomodate Review Revision Restructure
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
            var apiRevisionsManager = testsBaseFixture.APIRevisionManager;
            var user = testsBaseFixture.User;
            var review = await testsBaseFixture.ReviewManager.CreateReviewAsync(user, fileNameA, "Revision1", fileStreamA, false, "Swagger", true);
            await apiRevisionsManager.AddAPIRevisionAsync(user, review.ReviewId, fileNameB, "Revision2", fileStreamB, "Swagger", true);
            review = await reviewManager.GetReviewAsync(user, review.ReviewId);
            var headingWithDiffInSections = review.Revisions[0].HeadingsOfSectionsWithDiff[review.Revisions[1].RevisionId];
            Assert.All(headingWithDiffInSections,
                item => Assert.Contains(item, new int[] { 2, 16 }));
        }

        [Fact]
        public async Task AddRevisionAsync_Computes_Headings_Of_Sections_With_Diff_B()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var apiRevisionsManager = testsBaseFixture.APIRevisionManager;
            var user = testsBaseFixture.User;
            var review = await reviewManager.CreateReviewAsync(user, fileNameC, "Azure.Analytics.Purview.Account", fileStreamC, false, "Swagger", true);
            await apiRevisionsManager.AddAPIRevisionAsync(user, review.ReviewId, fileNameD, "Azure.Analytics.Purview.Account", fileStreamD, "Swagger", true);
            review = await reviewManager.GetReviewAsync(user, review.ReviewId);
            var headingWithDiffInSections = review.Revisions[0].HeadingsOfSectionsWithDiff[review.Revisions[1].RevisionId];
            Assert.All(headingWithDiffInSections,
                item => Assert.Contains(item, new int[] { 20, 275 }));
        }

        [Fact]
        public async Task Delete_PullRequest_Review_Throws_Exception()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var apiRevisionsManager = testsBaseFixture.APIRevisionManager;
            var user = testsBaseFixture.User;
            var review = await reviewManager.CreateReviewAsync(user, fileNameC, "Azure.Analytics.Purview.Account", fileStreamC, false, "Swagger", false);
            Assert.Equal(ReviewType.Manual, review.FilterType);

            review.FilterType = ReviewType.PullRequest;

            // Change review type to PullRequest
            await testsBaseFixture.ReviewRepository.UpsertReviewAsync(review);
            Assert.Equal(ReviewType.PullRequest, review.FilterType);

            await Assert.ThrowsAsync<UnDeletableReviewException>(async () => await apiRevisionsManager.SoftDeleteAPIRevisionAsync(user, review.ReviewId, review.Revisions[0].RevisionId));
        }
    }
}
#endif
