using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Repositories;

public interface ICosmosProjectRepository
{
    Task UpsertProjectAsync(Project project);
    Task<Project> GetProjectAsync(string projectId);
    Task<Project> GetProjectByCrossLanguagePackageIdAsync(string crossLanguagePackageId);
    Task<Project> GetProjectByExpectedPackageAsync(string language, string packageName);
}
