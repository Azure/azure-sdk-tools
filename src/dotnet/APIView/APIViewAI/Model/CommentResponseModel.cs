using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIViewAI.Model
{
    public class CommentResponseModel
    {
        public string Comment { get; set; }
        public string ReviewLine { get; set; }

        public CommentResponseModel(string comment, string reviewLine)
        {
            Comment = comment;
            ReviewLine = reviewLine;
        }
    }
}
