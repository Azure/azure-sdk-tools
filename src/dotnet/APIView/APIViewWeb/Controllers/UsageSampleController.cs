using System;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Controllers
{
    [Authorize("RequireOrganization")]
    public class UsageSampleController: Controller
    {
        private readonly UsageSampleManager _sampleManager;
        private readonly ReviewManager _reviewManager;

        public UsageSampleController(UsageSampleManager sampleManager, ReviewManager reviewManager)
        {
            _sampleManager = sampleManager;
            _reviewManager = reviewManager;
        }

        [HttpPost]
        public async Task Add(string reviewId, string sampleText)
        {
            await _sampleManager.CreateReviewUsageSampleAsync(reviewId, sampleText);
            var review = await _reviewManager.GetReviewAsync(User, reviewId);

        }

        //[HttpPost]
        //public async Task<ActionResult> Update(string reviewId, string commentId, string commentText)
        //{
        //    var comment =  await _commentsManager.UpdateCommentAsync(User, reviewId, commentId, commentText);

        //    return await CommentPartialAsync(reviewId, comment.ElementId);
        //}

        //[HttpPost]
        //public async Task<ActionResult> Delete(string reviewId, string commentId, string elementId)
        //{
        //    await _commentsManager.DeleteCommentAsync(User, reviewId, commentId);

        //    return await CommentPartialAsync(reviewId, elementId);
        //}

    }
}
