using System.Collections.Generic;

namespace APIViewWeb.LeanModels
{
    public class CommentFeedbackRequest
    {
        public List<string> Reasons { get; set; } = new();
        public string Comment { get; set; } = string.Empty;
        public bool IsDelete { get; set; } = false;
    }
}
