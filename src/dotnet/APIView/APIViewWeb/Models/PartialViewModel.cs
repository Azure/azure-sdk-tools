using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class PartialViewModel
    {
        public string AssemblyId { get; set; }
        public List<CommentModel> Comments { get; set; }
        public string LineId { get; set; }
    }
}
