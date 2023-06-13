using System;
using System.Collections.Generic;

namespace APIViewWeb.DTO
{
    public class CommentDto
    {
        public string CommentId { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string RevisionId { get; set; }
        public string ElementId { get; set; }
        public string Comment { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Username { get; set; }

        // add these features in the future

        //public bool IsResolve { get; set; }
        //public DateTime? EditedTimeStamp { get; set; } 
        //public List<string> Upvotes { get; set; } = new List<string>();
        //public HashSet<string> TaggedUsers { get; set; } = new HashSet<string>();
    }
}
