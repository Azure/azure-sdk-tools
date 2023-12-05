using System.Threading.Tasks;
using Xunit;
using System.IO;
using System;
using APIViewWeb.Repositories;
using APIViewWeb.LeanModels;
using System.Linq;

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
            var review = await testsBaseFixture.ReviewManager.CreateReviewAsync(packageName: "testPackageA", language: "Swagger", isClosed: false);

            await apiRevisionsManager.AddAPIRevisionAsync(user: user, reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, name: fileNameA,
                label: "Revision1", fileStream: fileStreamA, language: "Swagger", awaitComputeDiff: true);
            await apiRevisionsManager.AddAPIRevisionAsync(user: user, reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, name: fileNameB,
                label: "Revision2", fileStream: fileStreamB, language: "Swagger", awaitComputeDiff: true);

            var apiRevisions = (await apiRevisionsManager.GetAPIRevisionsAsync(review.Id)).ToList();

            var headingWithDiffInSections = apiRevisions[1].HeadingsOfSectionsWithDiff[apiRevisions[0].Id];
            Assert.All(headingWithDiffInSections,
                item => Assert.Contains(item, new int[] { 2, 16 }));
        }

        [Fact]
        public async Task AddRevisionAsync_Computes_Headings_Of_Sections_With_Diff_B()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var apiRevisionsManager = testsBaseFixture.APIRevisionManager;
            var user = testsBaseFixture.User;

            var review = await testsBaseFixture.ReviewManager.CreateReviewAsync(packageName: "testPackageB", language: "Swagger", isClosed: false);

            await apiRevisionsManager.AddAPIRevisionAsync(user: user, reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, name: fileNameC,
                label: "Azure.Analytics.Purview.Account", fileStream: fileStreamC, language: "Swagger", awaitComputeDiff: true);
            await apiRevisionsManager.AddAPIRevisionAsync(user: user, reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, name: fileNameD,
                label: "Azure.Analytics.Purview.Accoun", fileStream: fileStreamD, language: "Swagger", awaitComputeDiff: true);

            var apiRevisions = (await apiRevisionsManager.GetAPIRevisionsAsync(review.Id)).ToList();

            var headingWithDiffInSections = apiRevisions[1].HeadingsOfSectionsWithDiff[apiRevisions[0].Id];
            Assert.All(headingWithDiffInSections,
                item => Assert.Contains(item, new int[] { 20, 275 }));
        }

        [Fact]
        public async Task Delete_PullRequest_Review_Throws_Exception()
        {
            var reviewManager = testsBaseFixture.ReviewManager;
            var apiRevisionsManager = testsBaseFixture.APIRevisionManager;
            var user = testsBaseFixture.User;

            var review = await testsBaseFixture.ReviewManager.CreateReviewAsync(packageName: "testPackageC", language: "Swagger", isClosed: false);

            await apiRevisionsManager.AddAPIRevisionAsync(user: user, reviewId: review.Id, apiRevisionType: APIRevisionType.Manual, name: fileNameC,
                label: "Azure.Analytics.Purview.Account", fileStream: fileStreamC, language: "Swagger", awaitComputeDiff: true);

            var apiRevisions = (await apiRevisionsManager.GetAPIRevisionsAsync(review.Id)).ToList();

            Assert.Equal(APIRevisionType.Manual, apiRevisions[0].APIRevisionType);

            apiRevisions[0].APIRevisionType = APIRevisionType.PullRequest;

            // Change review type to PullRequest
            await testsBaseFixture.APIRevisionRepository.UpsertAPIRevisionAsync(apiRevisions[0]);
            Assert.Equal(APIRevisionType.PullRequest, apiRevisions[0].APIRevisionType);

            await Assert.ThrowsAsync<UnDeletableReviewException>(async () => await apiRevisionsManager.SoftDeleteAPIRevisionAsync(user, review.Id, apiRevisions[0].Id));
        }
    }
}
