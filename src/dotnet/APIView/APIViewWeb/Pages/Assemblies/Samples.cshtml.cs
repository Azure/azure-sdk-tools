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

        public string Endpoint { get; }
        public ReviewModel Review { get; private set; }
        public UsageSampleModel Sample { get; private set; }
        public string NewSampleContent { get; set; }

        public UsageSamplePageModel(
            IConfiguration configuration,
            UsageSampleManager samplesManager,
            ReviewManager reviewManager)
        {
            _samplesManager = samplesManager;
            _reviewManager = reviewManager;
            Endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
        }

        [FromForm]
        public UploadModel Upload { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "samples";
            Review = await _reviewManager.GetReviewAsync(User, id);
            Sample = await _samplesManager.GetReviewUsageSampleAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            var file = Upload.Files.SingleOrDefault();

            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    var reviewModel = await _samplesManager.CreateReviewUsageSampleAsync(Review.ReviewId, openReadStream);
                    return RedirectToPage();
                }
            }

            return RedirectToPage();
        }

    }
}
