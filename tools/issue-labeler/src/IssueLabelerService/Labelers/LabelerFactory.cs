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

        public LabelerFactory(ILogger<LabelerFactory> logger, IModelHolderFactoryLite modelHolderFactory, ILabelerLite labeler)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            _labeler = labeler;
        }

        public ILabeler GetLabeler(RepositoryConfiguration config) =>
            _labelers.GetOrAdd(
                config.LabelPredictor,
                key => new LegacyLabeler(_logger, _modelHolderFactory, _labeler, config)
            );
    }
}
