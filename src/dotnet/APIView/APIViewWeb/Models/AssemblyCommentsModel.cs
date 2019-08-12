using System.Linq;

namespace APIViewWeb.Models
{
    public class AssemblyCommentsModel
    {
        public string AssemblyId { get; set; }
        public CommentModel[] Comments { get; set; }
        public string Id { get; set; }

        public AssemblyCommentsModel() { }

        public AssemblyCommentsModel(string assemblyId, CommentModel commentModel, string id)
        {
            this.AssemblyId = assemblyId;
            this.Comments = new CommentModel[] { commentModel };
            this.Id = id;
        }

        public void AddComment(CommentModel comment)
        {
            Comments = Comments.Append(comment).ToArray();
        }

        public void DeleteComment(string id)
        {
            Comments = Comments.Where(comment => comment.Id != id).ToArray();
        }
    }
}
