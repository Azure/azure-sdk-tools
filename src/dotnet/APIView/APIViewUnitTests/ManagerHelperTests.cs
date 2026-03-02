using APIViewWeb;
using APIViewWeb.Helpers;
using Xunit;
using APIViewWeb.LeanModels;
using ApiView;
using APIViewWeb.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace APIViewUnitTests
{
    public class ManagerHelperTests
    {
        private class DummyLanguageService : LanguageService
        {
            private readonly string _name;
            private readonly bool _usesTreeStyleParser;
            public DummyLanguageService(string name, bool usesTreeStyleParser)
            {
                _name = name;
                _usesTreeStyleParser = usesTreeStyleParser;
            }
            public override string Name => _name;
            public override string[] Extensions => new string[0];
            public override string VersionString => "1.0";
            public override bool CanUpdate(string versionString) => true;
            public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null) => Task.FromResult<CodeFile>(null);
            public override bool UsesTreeStyleParser => _usesTreeStyleParser;
            public override CodeFile GetReviewGenPendingCodeFile(string fileName) => null;
            public override bool GeneratePipelineRunParams(APIRevisionGenerationPipelineParamModel param) => false;
            public override bool CanConvert(string versionString) => false;
        }

        private IConfiguration GetConfig(string host = null, string spaHost = null)
        {
            var dict = new Dictionary<string, string>();
            if (host != null) dict["APIVIew-Host-Url"] = host;
            if (spaHost != null) dict["APIVIew-SPA-Host-Url"] = spaHost;
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public void UpdateChangeHistory_Behaves_As_Expected()
        {
            var review = new ReviewListItemModel();
            Assert.Empty(review.ChangeHistory);

            // test_User_1 approves
            var updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, "test_User_1", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Single(review.ChangeHistory);
            Assert.True(updateResult.ChangeStatus);

            // test_User_1 reverts approval
            updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.ApprovalReverted, "test_User_1", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Equal(2, review.ChangeHistory.Count);
            Assert.False(updateResult.ChangeStatus);

            // test_User_2 Closed
            updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Closed, "test_User_2", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Equal(3, review.ChangeHistory.Count);
            Assert.True(updateResult.ChangeStatus);

            // test_User_2 approves
            updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, "test_User_2", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Equal(4, review.ChangeHistory.Count);
            Assert.True(updateResult.ChangeStatus);

            // test_User_3 approves 
            updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, "test_User_3", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Equal(5, review.ChangeHistory.Count);
            Assert.True(updateResult.ChangeStatus);

            // test_User_3 reverts approval 
            updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, "test_User_3", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Equal(6, review.ChangeHistory.Count);
            Assert.True(updateResult.ChangeStatus);

            // test_User_2 reverts approval
            updateResult = ChangeHistoryHelpers.UpdateBinaryChangeAction<ReviewChangeHistoryModel, ReviewChangeAction>(
                review.ChangeHistory, ReviewChangeAction.Approved, "test_User_2", "test_note");
            review.ChangeHistory = updateResult.ChangeHistory;
            Assert.Equal(7, review.ChangeHistory.Count);
            Assert.False(updateResult.ChangeStatus);

            Assert.True(review.ChangeHistory[0].ChangeAction == ReviewChangeAction.Approved && review.ChangeHistory[0].ChangedBy == "test_User_1");
            Assert.True(review.ChangeHistory[1].ChangeAction == ReviewChangeAction.ApprovalReverted && review.ChangeHistory[1].ChangedBy == "test_User_1");
            Assert.True(review.ChangeHistory[2].ChangeAction == ReviewChangeAction.Closed && review.ChangeHistory[2].ChangedBy == "test_User_2");
            Assert.True(review.ChangeHistory[3].ChangeAction == ReviewChangeAction.Approved && review.ChangeHistory[3].ChangedBy == "test_User_2");
            Assert.True(review.ChangeHistory[4].ChangeAction == ReviewChangeAction.Approved && review.ChangeHistory[4].ChangedBy == "test_User_3");
            Assert.True(review.ChangeHistory[5].ChangeAction == ReviewChangeAction.ApprovalReverted && review.ChangeHistory[5].ChangedBy == "test_User_3");
            Assert.True(review.ChangeHistory[6].ChangeAction == ReviewChangeAction.ApprovalReverted && review.ChangeHistory[6].ChangedBy == "test_User_2");
        }

        [Fact]
        public void Returns_Valid_Uri()
        {
            var config = GetConfig("https://host", "https://spa");
            var services = new List<LanguageService>
            {
                new DummyLanguageService("TestLanguage", false)
            };

            var url = ManagerHelpers.ResolveReviewUrl(
                reviewId: "rId",
                apiRevisionId: "aId",
                diffRevisionId: null,
                language: "TestLanguage",
                configuration: config,
                languageServices: services);

            Assert.Equal("https://host/Assemblies/Review/rId?revisionId=aId", url);

            url = ManagerHelpers.ResolveReviewUrl(
                reviewId: "rId",
                apiRevisionId: "aId",
                diffRevisionId: "dId",
                language: "TestLanguage",
                configuration: config,
                languageServices: services);

            Assert.Equal("https://host/Assemblies/Review/rId?revisionId=aId&diffRevisionId=dId", url);
        }

        [Fact]
        public void Returns_Valid_Spa_Uri()
        {
            var config = GetConfig("https://host", "https://spa");
            var services = new List<LanguageService>
        {
            new DummyLanguageService("TestLanguage", true)
        };

            var url = ManagerHelpers.ResolveReviewUrl(
                reviewId: "rId",
                apiRevisionId: "aId",
                diffRevisionId: null,
                language: "TestLanguage",
                configuration: config,
                languageServices: services);

            Assert.Equal("https://spa/review/rId?activeApiRevisionId=aId", url);

            url = ManagerHelpers.ResolveReviewUrl(
                reviewId: "rId",
                apiRevisionId: "aId",
                diffRevisionId: "dId",
                language: "TestLanguage",
                configuration: config,
                languageServices: services);

            Assert.Equal("https://spa/review/rId?activeApiRevisionId=aId&diffApiRevisionId=dId", url);
        }

        [Fact]
        public void Returns_Valid_Uri_With_ElementId()
        {
            var config = GetConfig("https://host", "https://spa");
            var services = new List<LanguageService>
            {
                new DummyLanguageService("TestLanguage", false)
            };

            var url = ManagerHelpers.ResolveReviewUrl(
                reviewId: "rId",
                apiRevisionId: "aId",
                language: "TestLanguage",
                configuration: config,
                languageServices: services,
                elementId: "Azure.Storage.BlobClient");

            Assert.Equal("https://host/Assemblies/Review/rId?revisionId=aId#Azure.Storage.BlobClient", url);
        }

        [Fact]
        public void Returns_Valid_Spa_Uri_With_ElementId()
        {
            var config = GetConfig("https://host", "https://spa");
            var services = new List<LanguageService>
            {
                new DummyLanguageService("TestLanguage", true)
            };

            var url = ManagerHelpers.ResolveReviewUrl(
                reviewId: "rId",
                apiRevisionId: "aId",
                language: "TestLanguage",
                configuration: config,
                languageServices: services,
                elementId: "Azure.Storage.BlobClient");

            Assert.Equal("https://spa/review/rId?activeApiRevisionId=aId&nId=Azure.Storage.BlobClient", url);
        }
    }
}
