using System.Collections.Generic;
using System.Linq;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Helpers
{
    /// <summary>
    /// Single source of truth for determining which comments are "visible" for a given
    /// active API revision. Used by the Conversations panel, badge counts, quality score,
    /// and review-page code panel.
    /// </summary>
    public static class CommentVisibilityHelper
    {
        /// <summary>
        /// Returns the set of comments that are relevant for the given active API revision.
        /// No resolved-status filtering or display caps are applied — consumers can layer
        /// those on top.
        /// </summary>
        public static List<CommentItemModel> GetVisibleComments(
            IEnumerable<CommentItemModel> allComments,
            string activeApiRevisionId)
        {
            // Only include comments for API revisions, not sample revisions
            return allComments
                .Where(c => c.CommentType != CommentType.SampleRevision)
                .ToList();
        }
    }
}
