using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IssueLabeler.Shared
{
    public class TriageOutput
    {
        public IEnumerable<string> Labels { get; set; }
        public string Answer { get; set; }
        public string AnswerType { get; set; }
    }
}
