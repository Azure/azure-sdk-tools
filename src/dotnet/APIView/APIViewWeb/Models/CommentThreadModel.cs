using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class CommentThreadModel
    {
        public string AssemblyId { get; set; }
        public List<CommentModel> Comments { get; set; }
        public string LineId { get; set; }
    }
}
