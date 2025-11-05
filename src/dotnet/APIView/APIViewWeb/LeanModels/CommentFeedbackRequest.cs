using System.Collections.Generic;

namespace APIViewWeb.LeanModels
{
    public class CommentFeedbackRequest
    {
        public List<string> Reasons { get; set; } = new();
        public string AdditionalComments { get; set; } = string.Empty;
    }
}
