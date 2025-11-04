using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public interface IIssueGeneratorService
    {
        public Task<string> GenerateIssue(string repositoryName);
    }
}
