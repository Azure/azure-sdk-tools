using IssueLabeler.Shared;

namespace LabelerFactory
{
    public interface ILabeler
    {
        public Task<string[]> PredictLabels(IssuePayload issue);
    }
}
