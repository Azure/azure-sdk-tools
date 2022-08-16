using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Markdig.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class UsageSamplePageModel : PageModel
    {
        private readonly UsageSampleManager _samplesManager;
        private readonly ReviewManager _reviewManager;
        private const string ENDPOINT_SETTING = "Endpoint";
        private readonly CommentsManager _commentsManager;
        private readonly NotificationManager _notificationManager;

        public string Endpoint { get; }
        public ReviewModel Review { get; private set; }
        public UsageSampleModel Sample { get; private set; }
        public CodeLineModel[] SampleContent { get; set; }
        public ReviewCommentsModel Comments { get; set; }

        public UsageSamplePageModel(
            IConfiguration configuration,
            UsageSampleManager samplesManager,
            ReviewManager reviewManager,
            CommentsManager commentsManager,
            NotificationManager notificationManager)
        {
            _samplesManager = samplesManager;
            _reviewManager = reviewManager;
            Endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _commentsManager = commentsManager;
            _notificationManager = notificationManager;
        }

        [FromForm]
        public UsageSampleUploadModel Upload { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "samples";
            Review = await _reviewManager.GetReviewAsync(User, id);
            Comments = await _commentsManager.GetUsageSampleCommentsAsync(id);
            //try
            //{
            //    Sample = await _samplesManager.GetReviewUsageSampleAsync(id);
            //    SampleContent = ParseLines(Sample.UsageSampleFileId, Comments).Result;
            //}
            //catch
            //{
            //    Sample = null;
            //    SampleContent = null;
            //}
            Sample = await _samplesManager.GetReviewUsageSampleAsync(id);
            SampleContent = ParseLines(Sample.UsageSampleFileId, Comments).Result;
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            var file = Upload.File;
            var sampleString = Upload.sampleString;
            var reviewId = Upload.ReviewId;
            var deleting = Upload.Deleting;

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    await _samplesManager.UpsertReviewUsageSampleAsync(User, reviewId, openReadStream);
                }
            }
            else if (sampleString != null || deleting)
            {
                await _samplesManager.UpsertReviewUsageSampleAsync(User, reviewId, sampleString);
            }

            return RedirectToPage();
        }

        private async Task<CodeLineModel[]> ParseLines(string fileId, ReviewCommentsModel comments)
        {
            if(Sample.UsageSampleFileId == null)
            {
                return new CodeLineModel[0];
            }

            string[] content = (await _samplesManager.GetUsageSampleContentAsync(fileId)).Split("\n");

            int skip = 0;
            while ((content.GetValue(content.Length - 1 - skip).ToString()) == "")
            {
                skip++;
            }
            
            CodeLineModel[] lines = new CodeLineModel[content.Length-skip];
            CodeDiagnostic[] cd = Array.Empty<CodeDiagnostic>();
            for (int i = 0; i < content.Length-skip; i++)
            {
                string lineContent = content[i];

                if(lineContent == "&nbsp;&nbsp;&nbsp;")
                {
                    continue;
                }

                var line = new CodeLine(lineContent, Sample.SampleId + "-line-" + (i+1).ToString() , "");
                comments.TryGetThreadForLine(Sample.SampleId + "-line-" + (i+1).ToString(), out var thread);
                lines[i] = new CodeLineModel(APIView.DIff.DiffLineKind.Unchanged, line, thread, cd, i + 1);
            }

            return Array.FindAll(lines, e => !(e.Diagnostics == null));
        }
    }
}
