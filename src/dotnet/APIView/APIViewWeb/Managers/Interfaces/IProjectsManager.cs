using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Managers.Interfaces;

public interface IProjectsManager
{
    Task<Project> UpsertProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata, ReviewListItemModel typeSpecReview);
    Task<Project> TryLinkReviewToProjectAsync(string userName, ReviewListItemModel review);
}
