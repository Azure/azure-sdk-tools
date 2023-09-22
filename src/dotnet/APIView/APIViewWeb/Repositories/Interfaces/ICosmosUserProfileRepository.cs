using APIViewWeb.Models;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosUserProfileRepository
    {
        public Task<UserProfileModel> TryGetUserProfileAsync(string UserName);
        public Task<Result> UpsertUserProfileAsync(ClaimsPrincipal User, UserProfileModel userModel);
    }
}
