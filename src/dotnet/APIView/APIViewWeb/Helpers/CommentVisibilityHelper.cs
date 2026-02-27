using System.Collections.Generic;
using System.Linq;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Helpers
{
    /// <summary>
    /// Single source of truth for determining which comments are "visible" for a given
    /// active API revision. Used by the Conversations panel, badge counts, quality score,
    /// and review-page code panel.
    ///
    /// Rules:
    ///  1. User comments (not diagnostic, not AI-generated): always visible regardless of revision.
    ///  2. AI-generated comments: always visible regardless of revision.
    ///  3. Diagnostic comments: only visible if they belong to the active revision.
    /// </summary>
    public static class CommentVisibilityHelper
    {
        /// <summary>
        /// Returns the set of comments that are relevant for the given active API revision.
        /// No resolved-status filtering or display caps are applied — consumers can layer
        /// those on top (e.g., code panel excludes resolved from other revisions;
        /// conversations panel caps diagnostics at 250 for display).
        /// </summary>
        public static List<CommentItemModel> GetVisibleComments(
            IEnumerable<CommentItemModel> allComments,
            string activeApiRevisionId)
        {
            var nonDiagnosticComments = allComments
                .Where(c => c.CommentSource != CommentSource.Diagnostic);

            var diagnosticCommentsForRevision = allComments
                .Where(c => c.CommentSource == CommentSource.Diagnostic
                         && c.APIRevisionId == activeApiRevisionId);

            return nonDiagnosticComments.Concat(diagnosticCommentsForRevision).ToList();
        }
    }
}
