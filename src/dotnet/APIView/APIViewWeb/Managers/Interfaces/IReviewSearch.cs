using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Managers.Interfaces
{
    public interface IReviewSearch
    { 
        Task<ResolvePackageResponse> ResolvePackageQuery(
            string packageQuery,
            string language,
            string version = null);
        Task<ResolvePackageResponse> ResolvePackageLink(string link);
    }
}
