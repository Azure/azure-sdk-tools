using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace IssueLabelerService
{
    public class IssueGeneratorFactory
    {
        private ILogger<IssueGeneratorFactory> _logger;
        private TriageRag _ragService;
        private ConcurrentDictionary<string, IIssueGeneratorService> _qnaServices = new();
        private BlobServiceClient _blobClient;

        public IssueGeneratorFactory(ILogger<IssueGeneratorFactory> logger, TriageRag ragService, BlobServiceClient blobClient)
        {
            _logger = logger;
            _ragService = ragService;
            _blobClient = blobClient;
        }

        public IIssueGeneratorService GetIssueGeneratorService(RepositoryConfiguration config) =>
            _qnaServices.GetOrAdd(
                config.AnswerService,
                key =>
                {
                    switch (key)
                    {
                        case "OpenAI":
                            return new OpenAiIssueGenerator(_logger, config, _ragService, _blobClient);
                        default:
                            _logger.LogWarning($"Unknown answer service type: {key} Running OpenAI.");
                            return new OpenAiIssueGenerator(_logger, config, _ragService, _blobClient);
                    }
                }
            );
    }
}
