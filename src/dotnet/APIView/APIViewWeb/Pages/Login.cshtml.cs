using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty(SupportsGet = true, Name = "returnurl")]
        public string ReturnUrl { get; set; } = "/";

        public IActionResult OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
                return Redirect(ReturnUrl);

            return Page();
        }
    }
}







//using System.Threading.Tasks;
//using APIViewWeb.Managers;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using System.Security.Claims;


//namespace APIViewWeb.Pages
//{
//    public class LoginModel : PageModel
//    {
//        //dependency injection bc method comes from UserProfileManager - 'I' because dependency is registered with interface
//        private readonly IUserProfileManager _userProfileManager;


//        public LoginModel(IUserProfileManager userProfileManager)
//        {
//            _userProfileManager = userProfileManager;
//        }


//        [BindProperty(SupportsGet = true, Name = "returnurl")]
//        public string ReturnUrl { get; set; } = "/";

//        public async Task<IActionResult> OnGetAsync()
//        {
//            if (User.Identity.IsAuthenticated)
//            {
//                await _userProfileManager.UpdateMicrosoftEmailInUserProfile(User as ClaimsPrincipal);
//                return Redirect(ReturnUrl);
//            }

//            return Page();
//        }
//    }
//}
