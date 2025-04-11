using System.Threading.Tasks;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public interface ILabeler
    {
        public Task<string[]> PredictLabels(IssuePayload issue);
    }
}
