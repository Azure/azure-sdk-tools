using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Models
{
    public class CommentPartialViewResultModel
    {
        public string ElementId { get; set; } 
        public string PartialViewResult { get; set; }

        public CommentPartialViewResultModel(string elementId, string partialViewResult) {
            this.ElementId = elementId;
            this.PartialViewResult = partialViewResult;
        }
    }
}
