// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public class PollingJobQueueManager : IPollingJobQueueManager
    {
        private readonly ConcurrentQueue<AIReviewJobInfoModel> _jobs = new();

        public void Enqueue(AIReviewJobInfoModel jobId)
        {
            _jobs.Enqueue(jobId);
        }

        public bool TryDequeue(out AIReviewJobInfoModel jobId)
        {
            return _jobs.TryDequeue(out jobId);
        }
    }
}
