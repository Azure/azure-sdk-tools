using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos;

namespace APIViewWeb.Pages.Assemblies
{
    public class UsageSamplePageModel : PageModel
    {
        private readonly ISamplesRevisionsManager _samplesRevisionsManager;
        private readonly IReviewManager _reviewManager;
        private readonly ICommentsManager _commentsManager;
        public readonly UserPreferenceCache _preferenceCache;
        private readonly IAuthorizationService _authorizationService;

        public ReviewListItemModel Review { get; private set; }
        public SamplesRevisionModel ActiveSampleRevision { get; private set; }
        public IEnumerable<SamplesRevisionModel> SampleRevisions { get; private set; }
        public CodeLineModel[] SampleContent { get; set; }
        public ReviewCommentsModel Comments { get; set; }
        public string SampleOriginal { get; set; }

        public UsageSamplePageModel(
            ISamplesRevisionsManager samplesRevisionsManager,
            IReviewManager reviewManager,
            ICommentsManager commentsManager,
            UserPreferenceCache preferenceCache, 
            IAuthorizationService authorizationService)
        {
            _samplesRevisionsManager = samplesRevisionsManager;
            _reviewManager = reviewManager;
            _commentsManager = commentsManager;
            _preferenceCache = preferenceCache;
            _authorizationService = authorizationService;
        }

        [FromForm]
        public SamplesRevisionUploadModel Upload { get; set; }

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "samples";
            Review = await _reviewManager.GetReviewAsync(User, id);

            await AssertAccess(User);

            // This try-catch is for the case that the deployment is set up incorrectly for usage samples
            try
            {
                SampleRevisions = await _samplesRevisionsManager.GetSamplesRevisionsAsync(Review.Id);

                if (SampleRevisions != null && SampleRevisions.Any())
                {
                    if (revisionId != null)
                    {
                        ActiveSampleRevision = SampleRevisions.Where(s => s.FileId == revisionId).First();
                    }
                    else
                    {
                        ActiveSampleRevision = SampleRevisions.First();
                    }

                    Comments = await _commentsManager.GetUsageSampleCommentsAsync(Review.Id);
                    SampleContent = ParseLines(ActiveSampleRevision.FileId, Comments).Result;
                    SampleOriginal = await _samplesRevisionsManager.GetSamplesRevisionContentAsync(ActiveSampleRevision.OriginalFileId);
                    Upload.updateString = SampleOriginal;
                    if (SampleContent == null)
                    {
                        // Potentially bad blob setup, potentially erroneous file fetch
                        ActiveSampleRevision.FileId = "File Content Missing";
                    }
                }
                else
                {
                    // Tests the blob response with a dummy file id 
                    string blobTest = await _samplesRevisionsManager.GetSamplesRevisionContentAsync("abdc");
                    if (blobTest == "Bad Blob")
                    {
                        throw new CosmosException(null, System.Net.HttpStatusCode.NotFound, 0, null, 0.0); // Error does not matter, only type, to ensure clean error page.
                    }

                    // No samples.
                    SampleRevisions = SampleRevisions ?? new List<SamplesRevisionModel>(); 
                    ActiveSampleRevision = new SamplesRevisionModel();
                    SampleContent = Array.Empty<CodeLineModel>();
                }
            }
            catch (CosmosException)
            {
                // Error gracefully
                ActiveSampleRevision = new SamplesRevisionModel();
                ActiveSampleRevision.FileId = "Bad Deployment";
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
            string revisionTitle = Upload.RevisionTitle;

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    await _samplesRevisionsManager.UpsertSamplesRevisionsAsync(User, reviewId, openReadStream, revisionTitle, file.FileName);
                }
            }
            else if (sampleString != null)
            {
                await _samplesRevisionsManager.UpsertSamplesRevisionsAsync(User, reviewId, sampleString, revisionTitle);
            }
            else if (Upload.Updating)
            {
                await _samplesRevisionsManager.UpsertSamplesRevisionsAsync(User, reviewId, Upload.updateString, revisionTitle);
            }
            else if (Upload.Deleting)
            {
                await _samplesRevisionsManager.DeleteSamplesRevisionAsync(User, reviewId, Upload.SampleId);
            }

            return RedirectToPage();
        }

        private async Task<CodeLineModel[]> ParseLines(string fileId, ReviewCommentsModel comments)
        {
            if(ActiveSampleRevision.FileId == null)
            {
                return new CodeLineModel[0];
            }

            string rawContent = (await _samplesRevisionsManager.GetSamplesRevisionContentAsync(fileId));
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

                var line = new CodeLine(lineContent, ActiveSampleRevision.FileId + "-line-" + (i+1-skipped).ToString() , "");
                comments.TryGetThreadForLine(ActiveSampleRevision.FileId + "-line-" + (i+1-skipped).ToString(), out var thread);
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
