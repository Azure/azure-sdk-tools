using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Octokit;

namespace APIViewWeb.Pages
{
    public class IndexModel : PageModel
    {
        public string GitHubAvatar { get; set; }

        public string GitHubLogin { get; set; }

        public string GitHubName { get; set; }

        public string GitHubUrl { get; set; }

        public IReadOnlyList<Repository> Repositories { get; set; }

        public void OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {
                GitHubName = User.FindFirst(c => c.Type == ClaimTypes.Name)?.Value;
                GitHubLogin = User.FindFirst(c => c.Type == "urn:github:login")?.Value;
                GitHubUrl = User.FindFirst(c => c.Type == "urn:github:url")?.Value;
                GitHubAvatar = User.FindFirst(c => c.Type == "urn:github:avatar")?.Value;
            }
        }
    }
}
