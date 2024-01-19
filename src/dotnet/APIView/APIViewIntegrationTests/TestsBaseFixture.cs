using System;
using System.Collections.Generic;
using APIViewWeb.Repositories;
using APIViewWeb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Moq;
using System.IO;
using System.Threading.Tasks;
using System.Security.Claims;
using Azure.Storage.Blobs.Models;
using APIView.Identity;
using APIViewWeb.Managers;
using APIViewWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using APIViewWeb.Managers.Interfaces;
using Microsoft.CodeAnalysis.Host;
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights;

namespace APIViewIntegrationTests
{
    public class TestsBaseFixture : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobContainerClient _blobCodeFileContainerClient;
        private readonly BlobContainerClient _blobOriginalContainerClient;
        
        public PackageNameManager PackageNameManager { get; private set; }
        public ReviewManager ReviewManager { get; private set; }
        public CommentsManager CommentsManager { get; private set; }
        public CodeFileManager CodeFileManager { get; private set; }
        public APIRevisionsManager APIRevisionManager { get; private set; }
        public BlobCodeFileRepository BlobCodeFileRepository { get; private set; }
        public CosmosReviewRepository ReviewRepository { get; private set; }
        public CosmosAPIRevisionsRepository APIRevisionRepository { get; private set; }
        public CosmosCommentsRepository CommentRepository { get; private set; }
        public ClaimsPrincipal User { get; private set; }
        public  string TestDataPath { get; private set; }

        public TestsBaseFixture()
        {
            var config = new ConfigurationBuilder()
               .AddEnvironmentVariables(prefix: "APIVIEW_")
               .AddUserSecrets(typeof(TestsBaseFixture).Assembly)
               .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddMemoryCache();
            services.AddSingleton<PackageNameManager>();
            services.AddSingleton<LanguageService, JsonLanguageService>();
            services.AddSingleton<LanguageService, CSharpLanguageService>();
            services.AddSingleton<LanguageService, CLanguageService>();
            services.AddSingleton<LanguageService, JavaLanguageService>();
            services.AddSingleton<LanguageService, PythonLanguageService>();
            services.AddSingleton<LanguageService, JavaScriptLanguageService>();
            services.AddSingleton<LanguageService, CppLanguageService>();
            services.AddSingleton<LanguageService, GoLanguageService>();
            services.AddSingleton<LanguageService, ProtocolLanguageService>();
            services.AddSingleton<LanguageService, SwaggerLanguageService>();
            services.AddSingleton<LanguageService, SwiftLanguageService>();
            services.AddSingleton<LanguageService, XmlLanguageService>();
            var serviceProvider = services.BuildServiceProvider();
            var memoryCache = serviceProvider.GetService<IMemoryCache>();
            var languageService = serviceProvider.GetServices<LanguageService>();
            PackageNameManager = serviceProvider.GetService<PackageNameManager>();
            User = TestUser.GetTestuser();

            _cosmosClient = new CosmosClient(config["Cosmos:ConnectionString"]);
            var dataBaseResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync("APIViewV2").Result;
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("APIRevisions", "/ReviewId").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Comments", "/ReviewId").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Profiles", "/id").Wait();
            ReviewRepository = new CosmosReviewRepository(config, _cosmosClient);
            APIRevisionRepository = new CosmosAPIRevisionsRepository(config, _cosmosClient);
            CommentRepository = new CosmosCommentsRepository(config, _cosmosClient);
            var cosmosUserProfileRepository = new CosmosUserProfileRepository(config, _cosmosClient);

            _blobCodeFileContainerClient = new BlobContainerClient(config["Blob:ConnectionString"], "codefiles");
            _blobOriginalContainerClient = new BlobContainerClient(config["Blob:ConnectionString"], "originals");
            _ = _blobCodeFileContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = _blobOriginalContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

            BlobCodeFileRepository = new BlobCodeFileRepository(config, memoryCache);
            var blobOriginalsRepository = new BlobOriginalsRepository(config);

            var authorizationServiceMoq = new Mock<IAuthorizationService>();
            authorizationServiceMoq.Setup(_ => _.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<Object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success);

            var telemetryClient = new Mock<TelemetryClient>();

            var notificationManager = new NotificationManager(config, ReviewRepository, cosmosUserProfileRepository, telemetryClient.Object);

            var devopsArtifactRepositoryMoq = new Mock<IDevopsArtifactRepository>();
            devopsArtifactRepositoryMoq.Setup(_ => _.DownloadPackageArtifact(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());

            devopsArtifactRepositoryMoq.Setup(_ => _.RunPipeline(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var signalRHubContextMoq = new Mock<IHubContext<SignalRHub>>();
            var options = new Mock<IOptions<OrganizationOptions>>();

            CommentsManager = new CommentsManager(
                 authorizationService: authorizationServiceMoq.Object, commentsRepository: CommentRepository,
                 notificationManager: notificationManager, options: options.Object);

            CodeFileManager = new CodeFileManager(
            languageServices: languageService, codeFileRepository: BlobCodeFileRepository,
            originalsRepository: blobOriginalsRepository, devopsArtifactRepository: devopsArtifactRepositoryMoq.Object);

            APIRevisionManager = new APIRevisionsManager(
                authorizationService: authorizationServiceMoq.Object, reviewsRepository: ReviewRepository,
                languageServices: languageService, devopsArtifactRepository: devopsArtifactRepositoryMoq.Object,
                codeFileManager: CodeFileManager, codeFileRepository: BlobCodeFileRepository, apiRevisionsRepository: APIRevisionRepository,
                originalsRepository: blobOriginalsRepository, notificationManager: notificationManager, signalRHubContext: signalRHubContextMoq.Object,
                telemetryClient: telemetryClient.Object);


            ReviewManager = new ReviewManager(
                authorizationService: authorizationServiceMoq.Object, reviewsRepository: ReviewRepository,
                apiRevisionsManager: APIRevisionManager, commentManager: CommentsManager, codeFileRepository: BlobCodeFileRepository,
                commentsRepository: CommentRepository, languageServices: languageService, signalRHubContext: signalRHubContextMoq.Object,
                telemetryClient: telemetryClient.Object);

            TestDataPath = config["TestPkgPath"];
        }

        public void Dispose()
        {
            _cosmosClient.GetDatabase("APIView").DeleteAsync().Wait();
            _cosmosClient.Dispose();

            _blobCodeFileContainerClient.DeleteIfExists();
            _blobOriginalContainerClient.DeleteIfExists();
        }
    }

    [CollectionDefinition("TestsBase Collection")]
    public class DatabaseCollection : ICollectionFixture<TestsBaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
