using APIViewWeb;
using Xunit;
using APIViewWeb.Repositories;
using Moq;
using System.IO;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using APIViewWeb.Hubs;
using System.Collections;
using System.Collections.Generic;
using APIViewWeb.Managers;
using System;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewUnitTests
{
    public class APIRevisionsManagerTests
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;

        public APIRevisionsManagerTests()
        {
            IAuthorizationService authorizationService = new Mock<IAuthorizationService>().Object;
            ICosmosReviewRepository cosmosReviewRepository = new Mock<ICosmosReviewRepository>().Object;
            ICosmosAPIRevisionsRepository cosmosAPIRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>().Object;
            IHubContext<SignalRHub> signalRHub = new Mock<IHubContext<SignalRHub>>().Object;
            IEnumerable<LanguageService> languageServices = new List<LanguageService>();
            IDevopsArtifactRepository devopsArtifactRepository = new Mock<IDevopsArtifactRepository>().Object;
            ICodeFileManager codeFileManager = new Mock<ICodeFileManager>().Object;
            IBlobCodeFileRepository blobCodeFileRepository = new Mock<IBlobCodeFileRepository>().Object;
            IBlobOriginalsRepository blobOriginalRepository = new Mock<IBlobOriginalsRepository>().Object;
            INotificationManager notificationManager = new Mock<INotificationManager>().Object;
            
            _apiRevisionsManager = new APIRevisionsManager(
                authorizationService: authorizationService, reviewsRepository: cosmosReviewRepository,
                apiRevisionsRepository: cosmosAPIRevisionsRepository, signalRHubContext: signalRHub,
                languageServices: languageServices, devopsArtifactRepository: devopsArtifactRepository,
                codeFileManager: codeFileManager, codeFileRepository: blobCodeFileRepository,
                originalsRepository: blobOriginalRepository, notificationManager: notificationManager);    
        }

        // GetLatestAPIRevisionsAsync

        [Fact]
        public async Task GetLatestAPIRevisionsAsyncThrowsExceptionWhenReviewIdAndAPIRevisionsAreAbsent()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await _apiRevisionsManager.GetLatestAPIRevisionsAsync(null, null));
        }

        [Fact]
        public async Task GetLatestAPIRevisionsAsyncReturnsDefaultIfNoLatestAPIRevisionIsFound()
        {
            var latest = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(apiRevisions: new List<APIRevisionListItemModel>());
            Assert.Equal(default(APIRevisionListItemModel), latest);
        }

        [Fact]
        public async Task GetLatestAPIRevisionsAsyncReturnsCorrectLatestWithAllTypesPresent()
        {
            var apiRevisions = new List<APIRevisionListItemModel>()
            {
                new APIRevisionListItemModel() { Id ="A", APIRevisionType = APIRevisionType.Manual, CreatedOn = DateTime.Now.AddMinutes(5) },
                new APIRevisionListItemModel() { Id ="B", APIRevisionType = APIRevisionType.Automatic, CreatedOn = DateTime.Now.AddMinutes(10) },
                new APIRevisionListItemModel() { Id ="C", APIRevisionType = APIRevisionType.PullRequest, CreatedOn = DateTime.Now.AddMinutes(15) },
            };
            var latest = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(apiRevisions: apiRevisions);
            Assert.Equal("C", latest.Id);

            var latestAutomatic = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(apiRevisions: apiRevisions, apiRevisionType: APIRevisionType.Automatic);
            Assert.Equal("B", latestAutomatic.Id);
        }

        [Fact]
        public async Task GetLatestAPIRevisionsAsyncReturnsCorrectLatestWhenSpecifiedTypeIsAbsent()
        {
            var apiRevisions = new List<APIRevisionListItemModel>()
            {
                new APIRevisionListItemModel() { Id ="A", APIRevisionType = APIRevisionType.Manual, CreatedOn = DateTime.Now.AddMinutes(5) },
                new APIRevisionListItemModel() { Id ="B", APIRevisionType = APIRevisionType.Automatic, CreatedOn = DateTime.Now.AddMinutes(10) },
            };

            var latest = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(apiRevisions: apiRevisions, apiRevisionType: APIRevisionType.PullRequest);
            Assert.Equal("B", latest.Id);
        }

    }
}
