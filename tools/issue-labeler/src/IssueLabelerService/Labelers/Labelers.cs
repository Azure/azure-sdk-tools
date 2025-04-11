using System.Collections.Concurrent;
using Hubbup.MikLabelModel;
using Microsoft.Extensions.Logging;

namespace IssueLabelerService
{
    public class Labelers
    {
        private ConcurrentDictionary<string, ILabeler> _labelers = new();
        private IModelHolderFactoryLite _modelHolderFactory;
        private ILogger<Labelers> _logger;
        private ILabelerLite _labeler;

        public Labelers(ILogger<Labelers> logger, IModelHolderFactoryLite modelHolderFactory, ILabelerLite labeler)
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
