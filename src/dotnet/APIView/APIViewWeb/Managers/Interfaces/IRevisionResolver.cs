using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers.Interfaces
{
    public class RevisionResolveResult
    {
        public string ReviewId { get; set; }
        public string RevisionId { get; set; }
    }

    public interface IRevisionResolver
    {
        Task<RevisionResolveResult> ResolveByPackageAsync(
            string packageQuery,
            string language,
            string version = null);
        Task<RevisionResolveResult> ResolveByLinkAsync(string link);
    }
}
