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

namespace APIViewIntegrationTests
{
    public class TestsBaseFixture : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobContainerClient _blobCodeFileContainerClient;
        private readonly BlobContainerClient _blobOriginalContainerClient;
        
        public PackageNameManager PackageNameManager { get; private set; }
        public ReviewManager ReviewManager { get; private set; }
        public BlobCodeFileRepository BlobCodeFileRepository { get; private set; }
        public CosmosReviewRepository ReviewRepository { get; private set; }
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
            var dataBaseResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync("APIView").Result;
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Comments", "/ReviewId");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Profiles", "/id");
            ReviewRepository = new CosmosReviewRepository(config, _cosmosClient);
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

            var notificationManager = new NotificationManager(config, ReviewRepository, cosmosUserProfileRepository);

            var devopsArtifactRepositoryMoq = new Mock<IDevopsArtifactRepository>();
            devopsArtifactRepositoryMoq.Setup(_ => _.DownloadPackageArtifact(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream());

            devopsArtifactRepositoryMoq.Setup(_ => _.RunPipeline(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var signalRHubContextMoq = new Mock<IHubContext<SignalRHub>>();

            ReviewManager = new ReviewManager(
                authorizationServiceMoq.Object, ReviewRepository, BlobCodeFileRepository, blobOriginalsRepository, CommentRepository,
                languageService, notificationManager, devopsArtifactRepositoryMoq.Object, PackageNameManager, signalRHubContextMoq.Object);

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
