using System;
using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public class LabelerFactory
    {
        private ConcurrentDictionary<string, ILabeler> _labelers = new();
        private IModelHolderFactoryLite _modelHolderFactory;
        private ILogger<LabelerFactory> _logger;
        private TriageRag _ragService;
        private BlobServiceClient _blobClient;

        public LabelerFactory(ILogger<LabelerFactory> logger, IModelHolderFactoryLite modelHolderFactory, TriageRag ragService, BlobServiceClient blobClient)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            _ragService = ragService;
            _blobClient = blobClient;
        }

        public ILabeler GetLabeler(RepositoryConfiguration config) =>
            _labelers.GetOrAdd(
                config.LabelPredictor,
                key =>
                {
                    switch (key)
                    {
                        case "OpenAI":
                            return new OpenAiLabeler(_logger, config, _ragService, _blobClient);
                        case "Legacy":
                            return new MLLabeler(_logger, _modelHolderFactory, config);
                        default:
                            _logger.LogWarning($"Unknown labeler type: {key} Running ML Labeler.");
                            return new MLLabeler(_logger, _modelHolderFactory, config);
                    }
                }
            );
    }
}
