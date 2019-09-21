using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class LegacyReview: PageModel
    {
        private readonly BlobCommentRepository commentRepository;

        public LegacyReview(BlobCommentRepository commentRepository)
        {
            this.commentRepository = commentRepository;
        }

        public string Id { get; set; }

        public Dictionary<string, List<CommentModel>> Comments { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            Id = id;
            Comments = new Dictionary<string, List<CommentModel>>();

            var assemblyComments = await commentRepository.FetchCommentsAsync(id);
            var comments = assemblyComments.Comments;

            foreach (var comment in comments)
            {
                if (!Comments.TryGetValue(comment.ElementId, out _))
                    Comments[comment.ElementId] = new List<CommentModel>() { comment };
                else
                    Comments[comment.ElementId].Add(comment);
            }

            return Page();
        }
    }
}
