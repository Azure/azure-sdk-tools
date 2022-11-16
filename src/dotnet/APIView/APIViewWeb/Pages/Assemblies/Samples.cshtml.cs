using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Services.FileContainer;

namespace APIViewWeb.Pages.Assemblies
{
    public class UsageSamplePageModel : PageModel
    {
        private readonly UsageSampleManager _samplesManager;
        private readonly ReviewManager _reviewManager;
        private readonly CommentsManager _commentsManager;
        public readonly UserPreferenceCache _preferenceCache;
        private readonly IAuthorizationService _authorizationService;

        public ReviewModel Review { get; private set; }
        public UsageSampleRevisionModel Sample { get; private set; }
        public IEnumerable<UsageSampleRevisionModel> SampleRevisions { get; private set; }
        public UsageSampleModel Samples { get; private set; }
        public CodeLineModel[] SampleContent { get; set; }
        public ReviewCommentsModel Comments { get; set; }
        public string SampleOriginal { get; set; }

        public UsageSamplePageModel(
            UsageSampleManager samplesManager,
            ReviewManager reviewManager,
            CommentsManager commentsManager,
            UserPreferenceCache preferenceCache, 
            IAuthorizationService authorizationService)
        {
            _samplesManager = samplesManager;
            _reviewManager = reviewManager;
            _commentsManager = commentsManager;
            _preferenceCache = preferenceCache;
            _authorizationService = authorizationService;
        }

        [FromForm]
        public UsageSampleUploadModel Upload { get; set; }

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "samples";
            Review = await _reviewManager.GetReviewAsync(User, id);

            await AssertAccess(User);

            // This try-catch is for the case that the deployment is set up incorrectly for usage samples
            try
            {
                Samples = (await _samplesManager.GetReviewUsageSampleAsync(id)).FirstOrDefault();
                if (Samples != null)
                {
                    SampleRevisions = Samples.Revisions.OrderByDescending(e => e.RevisionNumber).Where(e => !e.RevisionIsDeleted);
                }

                if (SampleRevisions != null && SampleRevisions.Any())
                {
                    if (revisionId != null)
                    {
                        Sample = SampleRevisions.Where(e => e.FileId == revisionId).First();
                    }
                    else
                    {
                        Sample = SampleRevisions.First();
                    }

                    Comments = await _commentsManager.GetUsageSampleCommentsAsync(Samples.ReviewId);
                    SampleContent = ParseLines(Sample.FileId, Comments).Result;
                    SampleOriginal = await _samplesManager.GetUsageSampleContentAsync(Sample.OriginalFileId);
                    Upload.updateString = SampleOriginal;
                    if (SampleContent == null)
                    {
                        // Potentially bad blob setup, potentially erroneous file fetch
                        Sample.FileId = "File Content Missing";
                    }
                }
                else
                {
                    // Tests the blob response with a dummy file id 
                    string blobTest = await _samplesManager.GetUsageSampleContentAsync("abdc");
                    if (blobTest == "Bad Blob")
                    {
                        throw new CosmosException(null, System.Net.HttpStatusCode.NotFound, 0, null, 0.0); // Error does not matter, only type, to ensure clean error page.
                    }

                    // No samples.
                    SampleRevisions = SampleRevisions ?? new List<UsageSampleRevisionModel>(); 
                    Sample = new UsageSampleRevisionModel(null, -1);
                    SampleContent = Array.Empty<CodeLineModel>();
                    Samples = Samples ?? new UsageSampleModel(Review.ReviewId);
                }
            }
            catch (CosmosException)
            {
                // Error gracefully
                Sample = new UsageSampleRevisionModel(null, -1);
                Sample.FileId = "Bad Deployment";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            await AssertAccess(User);

            var file = Upload.File;
            string sampleString = Upload.sampleString;
            string reviewId = Upload.ReviewId;
            int newRevNum = Upload.RevisionNumber+1;
            string revisionTitle = Upload.RevisionTitle;

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    await _samplesManager.UpsertReviewUsageSampleAsync(User, reviewId, openReadStream, newRevNum, revisionTitle, file.FileName);
                }
            }
            else if (sampleString != null)
            {
                await _samplesManager.UpsertReviewUsageSampleAsync(User, reviewId, sampleString, newRevNum, revisionTitle);
            }
            else if (Upload.Updating)
            {
                await _samplesManager.UpsertReviewUsageSampleAsync(User, reviewId, Upload.updateString, newRevNum, revisionTitle);
            }
            else if (Upload.Deleting)
            {
                await _samplesManager.DeleteUsageSampleAsync(User, reviewId, Upload.FileId, Upload.SampleId);
            }

            
            return RedirectToPage();
        }

        private async Task<CodeLineModel[]> ParseLines(string fileId, ReviewCommentsModel comments)
        {
            if(Sample.FileId == null)
            {
                return new CodeLineModel[0];
            }

            string rawContent = (await _samplesManager.GetUsageSampleContentAsync(fileId));
            if (rawContent == null || rawContent.Equals("Bad Blob"))
            {
                return null; // should only occur if there is a blob error or the file is removed by other means
            }
            rawContent = rawContent.Trim();
            string[] content = rawContent.Split("\n");
            
            CodeLineModel[] lines = new CodeLineModel[content.Length];
            CodeDiagnostic[] cd = Array.Empty<CodeDiagnostic>(); // Avoids errors
            int skipped = 0;
            for (int i = 0; i < content.Length; i++)
            {
                string lineContent = content[i];

                // remove the newlines before codeblocks
                if (lineContent.StartsWith("<div class=\"code") || lineContent.StartsWith("<div style=") || lineContent.StartsWith("<div class=\"lang"))
                {
                    lineContent = "";
                }

                // remove pre and closing div elements to clear more excess newlines
                lineContent = lineContent.Replace("<pre>", "").Replace("</pre>", "").Replace("</div>", "").Replace("\n", "");
                if (lineContent.Trim() == "")
                {
                    // Count lines skipped to keep indexing correct
                    skipped++;
                    continue;
                }

                // Allows the indent to work correctly for spacing purposes
                lineContent = "<div class=\"internal\">&nbsp;&nbsp;&nbsp;" + lineContent + "</div>";

                var line = new CodeLine(lineContent, Sample.FileId + "-line-" + (i+1-skipped).ToString() , "");
                comments.TryGetThreadForLine(Sample.FileId + "-line-" + (i+1-skipped).ToString(), out var thread);
                lines[i] = new CodeLineModel(APIView.DIff.DiffLineKind.Unchanged, line, thread, cd, i+1-skipped, new int[0]);
            }

            // Removes excess lines added that cause errors
            return Array.FindAll(lines, e => !(e.Diagnostics == null));
        }

        private async Task AssertAccess(ClaimsPrincipal user)
        {
            var auth = await _authorizationService.AuthorizeAsync(User, null, Startup.RequireOrganizationPolicy);
            if (!auth.Succeeded)
            {
                throw new AuthorizationFailedException();
            }

        }
    }
}
