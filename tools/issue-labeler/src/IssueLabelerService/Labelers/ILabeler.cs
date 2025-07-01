using System.Collections.Generic;
using System.Threading.Tasks;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public interface ILabeler
    {
        public Task<Dictionary<string, string>> PredictLabels(IssuePayload issue);
    }
}
