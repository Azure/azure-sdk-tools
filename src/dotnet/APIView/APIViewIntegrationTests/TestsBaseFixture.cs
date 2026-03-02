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
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Services;

namespace APIViewIntegrationTests
{
    public class TestsBaseFixture : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobContainerClient _blobCodeFileContainerClient;
        private readonly BlobContainerClient _blobOriginalContainerClient;
        private IConfigurationRoot _config;
        private readonly string _cosmosDBname = "ManagerTestsDB";
        
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
            _config = new ConfigurationBuilder()
               .AddEnvironmentVariables(prefix: "APIVIEW_")
               .AddUserSecrets(typeof(TestsBaseFixture).Assembly)
               .Build();

            _config["CosmosDBName"] = _cosmosDBname;

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_config);
            services.AddMemoryCache();
            services.AddSingleton<PackageNameManager>();
            services.AddSingleton<LanguageService, JsonLanguageService>();
            services.AddSingleton<LanguageService, CSharpLanguageService>();
            services.AddSingleton<LanguageService, CLanguageService>();
            services.AddSingleton<LanguageService, JavaLanguageService>();
            services.AddSingleton<LanguageService, PythonLanguageService>();
            services.AddSingleton<LanguageService, JavaScriptLanguageService>();
            services.AddSingleton<LanguageService, RustLanguageService>();
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

            var credential = new ChainedTokenCredential(new AzureCliCredential(), new AzureDeveloperCliCredential());
            _cosmosClient = new CosmosClient(_config["CosmosEndpoint"], credential);
            var dataBaseResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync(_config["CosmosDBName"]).Result;
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("APIRevisions", "/ReviewId").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Comments", "/ReviewId").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Profiles", "/id").Wait();
            dataBaseResponse.Database.CreateContainerIfNotExistsAsync("PullRe", "/id").Wait();
            ReviewRepository = new CosmosReviewRepository(_config, _cosmosClient, Mock.Of<ILogger<CosmosReviewRepository>>());
            APIRevisionRepository = new CosmosAPIRevisionsRepository(_config, _cosmosClient, ReviewRepository);
            CommentRepository = new CosmosCommentsRepository(_config, _cosmosClient);
            var cosmosUserProfileRepository = new CosmosUserProfileRepository(_config, _cosmosClient);

            var blobServiceClient = new BlobServiceClient(new Uri(_config["StorageAccountUrl"]), credential);
            _blobCodeFileContainerClient = blobServiceClient.GetBlobContainerClient("codefiles");
            _blobOriginalContainerClient = blobServiceClient.GetBlobContainerClient("originals");
            _ = _blobCodeFileContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = _blobOriginalContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

            BlobCodeFileRepository = new BlobCodeFileRepository(blobServiceClient, memoryCache, Mock.Of<ILogger<BlobCodeFileRepository>>());
            var blobOriginalsRepository = new BlobOriginalsRepository(blobServiceClient, Mock.Of<ILogger<BlobOriginalsRepository>>());

            var authorizationServiceMoq = new Mock<IAuthorizationService>();
            authorizationServiceMoq.Setup(_ => _.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<Object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success);

            var telemetryClient = new Mock<TelemetryClient>();

            var emailTemplateServiceMoq = new Mock<IEmailTemplateService>();
            var httpClientFactoryMoq = new Mock<IHttpClientFactory>();
            var loggerMoq = new Mock<ILogger<NotificationManager>>();
            var userProfileManagerMoq = new Mock<IUserProfileManager>();
            var userProfileCache = new UserProfileCache(memoryCache, userProfileManagerMoq.Object, Mock.Of<ILogger<UserProfileCache>>());
            var languageServicesMoq = new Mock<IEnumerable<LanguageService>>();
            var notificationPermissionsManagerMoq = new Mock<IPermissionsManager>();
            
            var notificationManager = new NotificationManager(_config, ReviewRepository, APIRevisionRepository, cosmosUserProfileRepository, userProfileCache, telemetryClient.Object, emailTemplateServiceMoq.Object, httpClientFactoryMoq.Object, loggerMoq.Object, languageServicesMoq.Object, notificationPermissionsManagerMoq.Object);

            var devopsArtifactRepositoryMoq = new Mock<IDevopsArtifactRepository>();
            devopsArtifactRepositoryMoq.Setup(_ => _.DownloadPackageArtifact(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());

            devopsArtifactRepositoryMoq.Setup(_ => _.RunPipeline(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var signalRHubContextMoq = new Mock<IHubContext<SignalRHub>>();
            var options = new Mock<IOptions<OrganizationOptions>>();

            var backgroundTaskQueueMoq = new Mock<IBackgroundTaskQueue>();
            var commentsLoggerMoq = new Mock<ILogger<CommentsManager>>();
            var copilotAuthServiceMoq = new Mock<ICopilotAuthenticationService>();
            var permissionsManagerMoq = new Mock<IPermissionsManager>();

            var diagnosticCommentService = new DiagnosticCommentService(CommentRepository);
            
            CommentsManager = new CommentsManager(
                apiRevisionsManager: APIRevisionManager,
                diagnosticCommentService: diagnosticCommentService,
                authorizationService: authorizationServiceMoq.Object,
                commentsRepository: CommentRepository,
                reviewRepository: ReviewRepository,
                notificationManager: notificationManager,
                codeFileRepository: BlobCodeFileRepository,
                signalRHubContext: signalRHubContextMoq.Object,
                httpClientFactory: httpClientFactoryMoq.Object,
                userProfileCache: userProfileCache,
                configuration: _config,
                options: options.Object,
                backgroundTaskQueue: backgroundTaskQueueMoq.Object,
                permissionsManager: permissionsManagerMoq.Object,
                copilotAuthService: copilotAuthServiceMoq.Object,
                logger: commentsLoggerMoq.Object);

            var codeFileManagerLoggerMoq = new Mock<ILogger<CodeFileManager>>();
            CodeFileManager = new CodeFileManager(
                languageServices: languageService, codeFileRepository: BlobCodeFileRepository,
                originalsRepository: blobOriginalsRepository, devopsArtifactRepository: devopsArtifactRepositoryMoq.Object,
                logger: codeFileManagerLoggerMoq.Object);

            APIRevisionManager = new APIRevisionsManager(
                authorizationService: authorizationServiceMoq.Object, reviewsRepository: ReviewRepository,
                languageServices: languageService, devopsArtifactRepository: devopsArtifactRepositoryMoq.Object,
                codeFileManager: CodeFileManager, codeFileRepository: BlobCodeFileRepository, apiRevisionsRepository: APIRevisionRepository,
                diagnosticCommentService: diagnosticCommentService,
                originalsRepository: blobOriginalsRepository, notificationManager: notificationManager, signalRHubContext: signalRHubContextMoq.Object,
                telemetryClient: telemetryClient.Object, projectsManager: Mock.Of<IProjectsManager>(), configuration: _config);

            var pollingJobQueueManagerMoq = new Mock<IPollingJobQueueManager>();
            var pullRequestsRepositoryMoq = new Mock<ICosmosPullRequestsRepository>();
            var reviewManagerLoggerMoq = new Mock<ILogger<ReviewManager>>();
            ReviewManager = new ReviewManager(
                authorizationService: authorizationServiceMoq.Object,
                reviewsRepository: ReviewRepository,
                apiRevisionsManager: APIRevisionManager,
                commentManager: CommentsManager,
                codeFileRepository: BlobCodeFileRepository,
                commentsRepository: CommentRepository,
                apiRevisionsRepository: APIRevisionRepository,
                signalRHubContext: signalRHubContextMoq.Object,
                languageServices: languageService,
                telemetryClient: telemetryClient.Object,
                codeFileManager: CodeFileManager,
                configuration: _config,
                httpClientFactory: httpClientFactoryMoq.Object,
                pollingJobQueueManager: pollingJobQueueManagerMoq.Object,
                notificationManager: notificationManager,
                pullRequestsRepository: pullRequestsRepositoryMoq.Object,
                copilotAuthService: copilotAuthServiceMoq.Object,
                logger: reviewManagerLoggerMoq.Object);

            TestDataPath = _config["TestPkgPath"];
        }

        public void Dispose()
        {
            _cosmosClient.GetDatabase(_cosmosDBname).DeleteAsync().Wait();
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
