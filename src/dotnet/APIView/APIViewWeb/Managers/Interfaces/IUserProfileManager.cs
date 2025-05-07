using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IUserProfileManager
    {
        public Task<UserProfileModel> TryGetUserProfileAsync(ClaimsPrincipal User);
        public Task<UserProfileModel> TryGetUserProfileByNameAsync(string UserName);
        public Task UpdateUserProfile(string userName, UserProfileModel profile);
        public Task SetUserEmailIfNullOrEmpty(ClaimsPrincipal User);
    }
}
