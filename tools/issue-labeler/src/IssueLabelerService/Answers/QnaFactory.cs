using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IssueLabelerService
{
    public class QnaFactory
    {
        private ILogger<QnaFactory> _logger;
        private TriageRag _ragService;
        private ConcurrentDictionary<string, IQnaService> _qnaServices = new();

        public QnaFactory(ILogger<QnaFactory> logger, TriageRag ragService)
        {
            _logger = logger;
            _ragService = ragService;
        }

        public IQnaService GetQna(RepositoryConfiguration config) =>
            _qnaServices.GetOrAdd(
                config.QnaService,
                key => new RagQna(_logger, config, _ragService)
            );
    }
}
