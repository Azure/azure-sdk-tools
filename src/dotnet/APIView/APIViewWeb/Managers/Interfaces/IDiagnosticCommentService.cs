// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers.Interfaces
{
    public interface IDiagnosticCommentService
    {
        Task<DiagnosticSyncResult> SyncDiagnosticCommentsAsync(
            string reviewId,
            string apiRevisionId,
            string currentDiagnosticsHash,
            CodeDiagnostic[] diagnostics,
            IEnumerable<CommentItemModel> existingComments);
    }

    public class DiagnosticSyncResult
    {
        public List<CommentItemModel> Comments { get; set; } = [];
        public string DiagnosticsHash { get; set; }
        // Indicates whether synchronization was performed (true) or skipped due to matching hash (false).
        public bool WasSynced { get; set; }
    }
}
