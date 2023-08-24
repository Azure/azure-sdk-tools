using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IUserProfileManager
    {
        public Task CreateUserProfileAsync(ClaimsPrincipal User, string Email, HashSet<string> Langauges = null, UserPreferenceModel Preferences = null);
        public Task<UserProfileModel> TryGetUserProfileAsync(ClaimsPrincipal User);
        public Task<UserProfileModel> TryGetUserProfileByNameAsync(string UserName);
        public Task UpdateUserPreferences(ClaimsPrincipal User, UserPreferenceModel preferences);
        public Task UpdateUserProfile(ClaimsPrincipal User, string email, HashSet<string> languages, UserPreferenceModel preferences);
    }
}
