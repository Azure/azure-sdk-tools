using System.Collections.Generic;
using System.Security.Claims;

namespace APIViewWeb.Models
{
    public class UserModel
    {
        public UserModel(ClaimsPrincipal User, string email, HashSet<string> languages)
        {
            UserName = User.GetGitHubLogin();
            Email = email;
            if(languages != null)
            {
                Languages = languages;
            }
            else
            {
                Languages = new HashSet<string>();
            }
            
        }

        public string UserName { get; set; }
        public string Email { get; set; }

        // Approvers only
        public HashSet<string> Languages { get; set; }

    }
}
