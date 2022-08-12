using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        public string SampleContent { get; set; }
        public CommentThreadModel Thread { get; set; }

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
            Sample = await _samplesManager.GetReviewUsageSampleAsync(id);
            SampleContent = await _samplesManager.GetUsageSampleContentAsync(Sample.UsageSampleFileId);
            var comments = await _commentsManager.GetReviewCommentsAsync(id);
            Thread = ParseThread(comments.Threads);
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

        private CommentThreadModel ParseThread(IEnumerable<CommentThreadModel> threads)
        {
            var comments = new List<CommentModel>();

            foreach (var thread in threads)
            {
                foreach (var comment in thread.Comments)
                {
                    if (comment.IsSampleComment)
                    {
                        if(comment.SampleId == Sample.SampleId)
                        {
                            comments.Add(comment);
                        }
                    }
                    
                }
            }
            return new CommentThreadModel(Review.ReviewId, Sample.SampleId, comments);
        }
    }
}
