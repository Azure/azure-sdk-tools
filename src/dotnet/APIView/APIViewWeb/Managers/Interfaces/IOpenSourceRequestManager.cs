using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IOpenSourceRequestManager
    {
        public Task<OpenSourceUserInfo> GetUserInfo(string githubUserId);

        public Task<bool> IsAuthorizedUser(string githubUserId);
    }
}
