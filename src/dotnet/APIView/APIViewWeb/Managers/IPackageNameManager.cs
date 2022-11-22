using APIViewWeb.Models;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public interface IPackageNameManager
    {
        public Task<PackageModel> GetPackageDetails(string packageName);

    }
}
