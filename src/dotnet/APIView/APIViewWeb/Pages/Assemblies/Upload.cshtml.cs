using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class UploadModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;
        private readonly BlobCommentRepository comments;

        public UploadModel(BlobAssemblyRepository assemblyRepository, BlobCommentRepository comments)
        {
            this.assemblyRepository = assemblyRepository;
            this.comments = comments;
        }

        [BindProperty]
        public bool KeepOriginal { get; set; } 

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (file != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    
                    memoryStream.Position = 0;

                    AssemblyModel assemblyModel = new AssemblyModel();
                    assemblyModel.Author = User.GetGitHubLogin();
                    assemblyModel.HasOriginal = true;
                    assemblyModel.OriginalFileName = file.FileName;
                    assemblyModel.TimeStamp = DateTime.UtcNow;
                    AnalysisResult[] analysisResults = assemblyModel.BuildFromStream(memoryStream);

                    AssemblyCommentsModel analysisComments = new AssemblyCommentsModel();
                    analysisComments.Comments = Array.Empty<CommentModel>();
                    foreach (var result in analysisResults) {
                        var comment = new CommentModel();
                        comment.Comment = FormatComment(result);
                        comment.ElementId = result.TargetId;
                        comment.Id = Guid.NewGuid().ToString();
                        comment.TimeStamp = DateTime.UtcNow;
                        comment.Username = "dotnet-bot";
                        analysisComments.AddComment(comment);                    
                    }

                    memoryStream.Position = 0;

                    var originalStream = KeepOriginal ? memoryStream : null;

                    var id = await assemblyRepository.UploadAssemblyAsync(assemblyModel, originalStream);

                    analysisComments.AssemblyId = id;
                    await comments.UploadCommentsAsync(analysisComments);

                    return RedirectToPage("Review", new { id });
                }
            }

            return RedirectToPage("./Index");
        }

        private string FormatComment(AnalysisResult result)
        {
            var builder = new StringBuilder();
            if (result.Text.StartsWith("DO")) {
                builder.Append("<font color=\"green\">✅</font><strong>DO</strong>");
                builder.Append(result.Text.Substring(2));
            }
            else {
                builder.Append(result.Text);
            }

            if (!string.IsNullOrEmpty(result.HelpLinkUri)) {
                builder.Append($" [<a href={result.HelpLinkUri}>details</a>]");
            }
            return builder.ToString();
        }
    }
}
