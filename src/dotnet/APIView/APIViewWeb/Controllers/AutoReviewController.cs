// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Filters;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Controllers
{
    [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
    public class AutoReviewController : Controller
    {
        private readonly ReviewManager _reviewManager;

        public AutoReviewController(ReviewManager reviewManager)
        {
            _reviewManager = reviewManager;
        }

        [HttpPost]
        public async Task<ActionResult> UploadAutoReview([FromForm] IFormFile file, string label)
        {
            if (file != null)
            {
                using (var openReadStream = file.OpenReadStream())
                {
                    var review = await _reviewManager.CreateMasterReviewAsync(User, file.FileName, label, openReadStream, false);
                    if(review != null)
                    {
                        var reviewUrl = $"{this.Request.Scheme}://{this.Request.Host}/Assemblies/Review/{review.ReviewId}";
                        //Return 200 OK if last revision is approved and 201 if revision is not yet approved.
                        var result = review.Revisions.Last().Approvers.Count > 0 ? Ok(reviewUrl) : StatusCode(statusCode: StatusCodes.Status201Created, reviewUrl);
                        return result;
                    }
                }
            }
            // Return internal server error for any unknown error
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
