using System;
using System.Collections.Concurrent;
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

        public LabelerFactory(ILogger<LabelerFactory> logger, IModelHolderFactoryLite modelHolderFactory, ILabelerLite labeler, TriageRag ragService)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            _labeler = labeler;
            _ragService = ragService;
        }

        public ILabeler GetLabeler(RepositoryConfiguration config) =>
            _labelers.GetOrAdd(
                config.LabelPredictor,
                key =>
                {
                    switch (key)
                    {
                        case "OpenAi":
                            return new OpenAiLabeler(_logger, config, _ragService);
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
