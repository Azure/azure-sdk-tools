using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public interface IQnaService
    {
        public Task<QnaOutput> AnswerQuery(IssuePayload issue);
    }
}
