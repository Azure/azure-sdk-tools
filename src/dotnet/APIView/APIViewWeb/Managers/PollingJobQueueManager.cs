// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using APIViewWeb.Models;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers
{
    public class PollingJobQueueManager : IPollingJobQueueManager
    {
        private readonly ConcurrentQueue<AIReviewJobInfoModel> _jobs = new();
        private readonly ILogger<PollingJobQueueManager> _logger;

        public PollingJobQueueManager(ILogger<PollingJobQueueManager> logger)
        {
            _logger = logger;
        }

        public void Enqueue(AIReviewJobInfoModel jobId)
        {
            _jobs.Enqueue(jobId);
            _logger.LogInformation("Enqueued Copilot job. JobId: {JobId}, ReviewId: {ReviewId}, Queue size: {QueueSize}",
                jobId.JobId, jobId.APIRevision.ReviewId, _jobs.Count);
        }

        public bool TryDequeue(out AIReviewJobInfoModel jobId)
        {
            var result = _jobs.TryDequeue(out jobId);
            if (result)
            {
                _logger.LogDebug("Dequeued Copilot job. JobId: {JobId}, Remaining in queue: {QueueSize}",
                    jobId.JobId, _jobs.Count);
            }
            return result;
        }
    }
}
