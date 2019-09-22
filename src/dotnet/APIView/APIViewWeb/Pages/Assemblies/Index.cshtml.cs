using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly CosmosReviewRepository _cosmosReviewRepository;

        public IndexPageModel(CosmosReviewRepository cosmosReviewRepository)
        {
            _cosmosReviewRepository = cosmosReviewRepository;
        }

        public IEnumerable<ReviewModel> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            Assemblies = await _cosmosReviewRepository.GetReviewsAsync();
        }
    }
}
