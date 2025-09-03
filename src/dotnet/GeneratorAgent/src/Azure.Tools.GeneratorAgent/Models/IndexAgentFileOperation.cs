using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Models
{
    internal class IndexAgentFileOperation
    {
        private readonly PersistentAgentsClient _client;
        private string _fileId;
        private PersistentAgentFileInfo? _fileInfo;
        private ILogger<ErrorFixerAgent> _logger;

        public IndexAgentFileOperation(PersistentAgentsClient client, string fileId, ILogger<ErrorFixerAgent> logger)
        {
            _client = client;
            _fileId = fileId;
            _logger = logger;
        }

        public bool HasCompleted => _fileInfo?.Status != null && (
            _fileInfo.Status.Equals(FileState.Deleted) ||
            _fileInfo.Status.Equals(FileState.Processed)
        );

        public string FileId => _fileId;

        public PersistentAgentFileInfo Value => _fileInfo ?? throw new InvalidOperationException("The operation is not complete");

        public bool HasValue => _fileInfo != null;

        public async Task UpdateStatusAsync(CancellationToken cancellationToken = default)
        {
            if (!HasCompleted)
            {
                try
                {
                    Response<PersistentAgentFileInfo> response = await _client.Files.GetFileAsync(_fileId, cancellationToken).ConfigureAwait(false);
                    _fileInfo = response.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update status for file ID: {FileId}", _fileId);
                }
            }
        }

        public async ValueTask WaitForCompletionAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            while (!HasCompleted)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await UpdateStatusAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
