using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public class AnswerFactory
    {
        private ILogger<AnswerFactory> _logger;
        private TriageRag _ragService;
        private ConcurrentDictionary<string, IAnswerService> _qnaServices = new();

        public AnswerFactory(ILogger<AnswerFactory> logger, TriageRag ragService)
        {
            _logger = logger;
            _ragService = ragService;
        }

        public IAnswerService GetAnswerService(RepositoryConfiguration config) =>
            _qnaServices.GetOrAdd(
                config.AnswerService,
                key =>
                {
                    switch (key)
                    {
                        case "OpenAI":
                            return new OpenAiAnswerService(_logger, config, _ragService);
                        default:
                            _logger.LogWarning($"Unknown answer service type: {key} Running OpenAI.");
                            return new OpenAiAnswerService(_logger, config, _ragService);
                    }
                }
            );
    }
}

