using System.Collections.Concurrent;
using ConfigurationService;
using Hubbup.MikLabelModel;
using IssueLabeler.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LabelerFactory
{
    public class Labelers
    {
        private static ConcurrentDictionary<string, ILabeler> _labelers = new();
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
