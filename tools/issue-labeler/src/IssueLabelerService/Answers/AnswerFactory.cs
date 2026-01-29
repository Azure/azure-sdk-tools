using System.Collections.Concurrent;
using Azure.Core;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public class AnswerFactory
    {
        private ILogger<AnswerFactory> _logger;
        private TriageRag _ragService;
        private readonly TokenCredential _credential;
        private ConcurrentDictionary<string, IAnswerService> _qnaServices = new();

        public AnswerFactory(ILogger<AnswerFactory> logger, TriageRag ragService, TokenCredential credential)
        {
            _logger = logger;
            _ragService = ragService;
            _credential = credential;
        }

        public IAnswerService GetAnswerService(RepositoryConfiguration config) =>
            _qnaServices.GetOrAdd(
                config.AnswerService,
                key =>
                {
                    switch (key)
                    {
                        case "OpenAI":
                            return new OpenAiAnswerService(_logger, config, _ragService, _credential);
                        default:
                            _logger.LogWarning($"Unknown answer service type: {key} Running OpenAI.");
                            return new OpenAiAnswerService(_logger, config, _ragService, _credential);
                    }
                }
            );
    }
}

