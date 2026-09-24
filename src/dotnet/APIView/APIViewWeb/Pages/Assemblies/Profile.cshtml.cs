using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class ProfileModel : PageModel
    {
        public IActionResult OnGetAsync(string UserName)
        {
            var spaUrl = "https://spa." + Request.Host.ToString() + $"/profile/{UserName}";
            return Redirect(spaUrl);
        }
    }
}
