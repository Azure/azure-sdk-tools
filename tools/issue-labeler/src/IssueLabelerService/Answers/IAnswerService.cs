using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public interface IAnswerService
    {
        public Task<AnswerOutput> AnswerQuery(IssuePayload issue, Dictionary<string, string> labels);
    }
}
