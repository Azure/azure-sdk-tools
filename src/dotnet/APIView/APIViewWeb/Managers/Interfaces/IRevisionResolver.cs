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
        string ResolvePackageQuery(string packageQuery, string language);
        Task<RevisionResolveResult> ResolveByPackageAsync(
            string packageQuery,
            string language,
            string version = null);
        Task<RevisionResolveResult> ResolveByLinkAsync(string link);
    }
}
