using APIViewWeb.Models;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosUserProfileRepository
    {
        public Task<UserProfileModel> TryGetUserProfileAsync(string userName, bool createIfNotExist = true);
        public Task<Result> UpsertUserProfileAsync(ClaimsPrincipal user, UserProfileModel userModel);
        public Task<Result> UpsertUserProfileAsync(string userName, UserProfileModel userModel);
        public Task<IEnumerable<string>> GetAllUsernamesAsync();
        public Task<IEnumerable<string>> GetExistingUsersAsync(IEnumerable<string> userNames);
    }
}
