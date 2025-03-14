using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIViewAI.Model
{
    public class CommentRequestModel
    {
        public string Comment;
        public string ReviewLine;
        public string APISurface;

        public CommentRequestModel(string comment, string reviewLine, string apisurface)
        {
            Comment = comment;
            ReviewLine = reviewLine;
            APISurface = apisurface;
        }
    }
}
