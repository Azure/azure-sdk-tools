using System.Threading.Tasks;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public interface IIssueGeneratorService
    {
        public Task<string> GenerateIssue(IssueGeneratorPayload payload);
    }
}
