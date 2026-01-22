using System;
using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Hubbup.MikLabelModel;
using Microsoft.Extensions.Logging;

namespace IssueLabelerService
{
    public class LabelerFactory
    {
        private ConcurrentDictionary<string, ILabeler> _labelers = new();
        private IModelHolderFactoryLite _modelHolderFactory;
        private ILogger<LabelerFactory> _logger;
        private ILabelerLite _labeler;
        private TriageRag _ragService;
        private readonly McpTriageRag _mcpRagService;
        private BlobServiceClient _blobClient;

        public LabelerFactory(
            ILogger<LabelerFactory> logger,
            IModelHolderFactoryLite modelHolderFactory,
            ILabelerLite labeler,
            TriageRag ragService,
            McpTriageRag mcpRagService,
            BlobServiceClient blobClient)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            _labeler = labeler;
            _ragService = ragService;
            _mcpRagService = mcpRagService;
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
                        case "McpOpenAI":
                            return new McpOpenAiLabeler(_logger, config, _mcpRagService, _ragService, _blobClient);
                        case "Legacy":
                            return new LegacyLabeler(_logger, _modelHolderFactory, _labeler, config);
                        default:
                            _logger.LogWarning($"Unknown labeler type: {key} Running Legacy.");
                            return new LegacyLabeler(_logger, _modelHolderFactory, _labeler, config);
                    }
                }
            );
    }
}
