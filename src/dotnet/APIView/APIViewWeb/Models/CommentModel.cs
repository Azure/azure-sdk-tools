using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Models
{
    public class CommentModel
    {
        public string Id { get; set; }
        public string ElementId { get; set; }
        public string Comment { get; set; }
    }
}
