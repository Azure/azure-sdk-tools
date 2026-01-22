using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Managers.Interfaces
{
    public interface IResolvePackage
    { 
        Task<ResolvePackageResponse> ResolvePackageQuery(
            string packageQuery,
            string language,
            string version = null);
        Task<ResolvePackageResponse> ResolvePackageLink(string link);
    }
}
