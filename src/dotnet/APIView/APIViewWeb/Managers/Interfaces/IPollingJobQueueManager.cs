using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IPollingJobQueueManager
    {
        public void Enqueue(AIReviewJobInfoModel jobId);
        public bool TryDequeue(out AIReviewJobInfoModel jobId);
    }
}
