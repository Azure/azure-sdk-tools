using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IssueLabeler.Shared
{
    public class IssuePayload
    {
        public int IssueNumber { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string IssueUserLogin { get; set; }
        public string RepositoryName { get; set; }
        public string RepositoryOwnerName { get; set; }
        public bool DisableLabels { get; set; }
        public bool DisableAnswers { get; set; }
    }
}
