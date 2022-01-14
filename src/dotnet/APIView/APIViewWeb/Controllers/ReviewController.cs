using System;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Controllers
{
    [Authorize("RequireOrganization")]
    public class ReviewController : Controller
    {
        private readonly ReviewManager _reviewManager;

        public ReviewController(ReviewManager reviewManager)
        {
            _reviewManager = reviewManager;
        }

        [HttpGet]
        public async Task<string> GetReviews()
        {
            return await _reviewManager.GetReviewsJsonAsync();
        }
    }
}
