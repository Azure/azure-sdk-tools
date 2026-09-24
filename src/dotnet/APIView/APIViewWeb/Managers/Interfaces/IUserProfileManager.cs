using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IUserProfileManager
    {
        public Task<UserProfileModel> TryGetUserProfileAsync(ClaimsPrincipal User);
        public Task<UserProfileModel> TryGetUserProfileByNameAsync(string UserName, bool createIfNotExist = true);
        public Task UpdateUserProfile(string userName, UserProfileModel profile);
    }
}
