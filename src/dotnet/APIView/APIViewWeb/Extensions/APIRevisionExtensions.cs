using System;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;

namespace APIViewWeb.Extensions
{
    public static class APIRevisionExtensions
    {
        /// <summary>
        /// Evaluates and persists HasDuplicateLineIds for revisions that were never evaluated.
        /// Persistence is best-effort; failures are swallowed so callers can still return content.
        /// </summary>
        public static async Task SelfHealHasDuplicateLineIdsAsync(
            this IAPIRevisionsManager revisionsManager,
            APIRevisionListItemModel revision,
            CodeFile codeFile)
        {
            if (revision.HasDuplicateLineIds != null)
                return;

            revision.HasDuplicateLineIds = CodeFileManager.HasDuplicateLineIds(codeFile);
            try
            {
                await revisionsManager.UpdateAPIRevisionAsync(revision);
            }
            catch (Exception)
            {
                // Best-effort persist; swallow transient Cosmos/network errors
                // so review content can still be returned to the caller.
            }
        }
    }
}
