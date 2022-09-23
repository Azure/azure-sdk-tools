using System.Collections.Generic;
using System.Security.Claims;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class UserProfileModel
    {
        // Default case
        public UserProfileModel() 
        {
            Languages = new HashSet<string>();
        }

        public UserProfileModel(ClaimsPrincipal User, string email, HashSet<string> languages)
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

        [JsonProperty("id")]
        public string UserName { get; set; }
        public string Email { get; set; }

        // Approvers only
        public HashSet<string> Languages { get; set; }

    }
}
