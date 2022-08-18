using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Markdig.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

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
        public List<UsageSampleModel> SampleRevisions { get; private set; }
        public CodeLineModel[] SampleContent { get; set; }
        public ReviewCommentsModel Comments { get; set; }
        public string SampleOriginal { get; set; }
        public int latestRevision { get; set; }

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

        public async Task<IActionResult> OnGetAsync(string id, string revisionId = null)
        {
            TempData["Page"] = "samples";
            Review = await _reviewManager.GetReviewAsync(User, id);
            Comments = await _commentsManager.GetUsageSampleCommentsAsync(id);
            latestRevision = -1;
            try
            {
                SampleRevisions = await _samplesManager.GetReviewUsageSampleAsync(id);
                if (SampleRevisions.Any())
                {
                    if (SampleRevisions.Count > 1)
                    {
                        for (int i = 0; i < SampleRevisions.Count; i++)
                        {
                            if (SampleRevisions[i].RevisionNum > latestRevision)
                            {
                                latestRevision = SampleRevisions[i].RevisionNum;
                            }
                        }

                        if (revisionId != null)
                        {
                            Sample = SampleRevisions.Find(e => e.SampleId == revisionId);
                        }
                        else
                        {
                            Sample = SampleRevisions.Find(e => e.RevisionNum == latestRevision);
                        }
                    }
                    else
                    {
                        Sample = SampleRevisions[0];
                        latestRevision = 0;
                    }

                    SampleContent = ParseLines(Sample.UsageSampleFileId, Comments).Result;
                    SampleOriginal = await _samplesManager.GetUsageSampleContentAsync(Sample.UsageSampleOriginalFileId);
                    Upload.updateString = SampleOriginal;
                    if (SampleContent == null)
                    {
                        Sample.SampleId = "Bad Deployment";
                    }
                }
                else
                {
                    Sample = new UsageSampleModel(null, Review.ReviewId, -1);
                    SampleContent = Array.Empty<CodeLineModel>();
                }
            }
            catch (CosmosException e)
            {
                Sample = new UsageSampleModel(null, null);
                Sample.SampleId = "Bad Deployment";
            }
            
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            var file = Upload.File;
            string sampleString = Upload.sampleString;
            string reviewId = Upload.ReviewId;
            int newRevNum = Upload.RevisionNumber+1;
            string revisionTitle = Upload.RevisionTitle;

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    await _samplesManager.UpsertReviewUsageSampleAsync(User, reviewId, openReadStream, newRevNum, revisionTitle);
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
                await _samplesManager.DeleteUsageSampleAsync(User, reviewId, Upload.SampleId);
            }

            
            return RedirectToPage();
        }

        private async Task<CodeLineModel[]> ParseLines(string fileId, ReviewCommentsModel comments)
        {
            if(Sample.UsageSampleFileId == null)
            {
                return new CodeLineModel[0];
            }

            string rawContent = (await _samplesManager.GetUsageSampleContentAsync(fileId));
            if (rawContent == null)
            {
                return null;
            }
            rawContent = rawContent.Trim();
            string[] content = rawContent.Split("\n");
            
            CodeLineModel[] lines = new CodeLineModel[content.Length];
            CodeDiagnostic[] cd = Array.Empty<CodeDiagnostic>();
            int skipped = 0;
            for (int i = 0; i < content.Length; i++)
            {
                string lineContent = content[i];

                if (lineContent.StartsWith("<div class="))
                {
                    lineContent = "";
                }
                lineContent = lineContent.Replace("<pre>", "").Replace("</pre>", "").Replace("</div>", "");
                if (lineContent.Trim() == "")
                {
                    skipped++;
                    continue;
                }

                var line = new CodeLine(lineContent, Sample.SampleId + "-line-" + (i+ 1-skipped).ToString() , "");
                comments.TryGetThreadForLine(Sample.SampleId + "-line-" + (i+ 1-skipped).ToString(), out var thread);
                lines[i] = new CodeLineModel(APIView.DIff.DiffLineKind.Unchanged, line, thread, cd, i+1-skipped);
            }

            return Array.FindAll(lines, e => !(e.Diagnostics == null));
        }
    }
}
