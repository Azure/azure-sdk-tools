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
            Preferences = new UserPreferenceModel();
        }

        public UserProfileModel(ClaimsPrincipal User, string email, HashSet<string> languages, UserPreferenceModel preferences)
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
            
            if(preferences != null)
            {
                Preferences = preferences;
            } else
            {
                Preferences = new UserPreferenceModel();
            }
        }

        [JsonProperty("id")]
        public string UserName { get; set; }
        public string Email { get; set; }

        // Approvers only
        public HashSet<string> Languages { get; set; }

        public UserPreferenceModel Preferences { get; set; }

    }
}
