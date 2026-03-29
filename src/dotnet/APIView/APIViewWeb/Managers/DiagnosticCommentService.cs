using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;

namespace APIViewWeb.Managers;

public class DiagnosticCommentService : IDiagnosticCommentService
{
    private readonly ICosmosCommentsRepository _commentsRepository;

    public DiagnosticCommentService(ICosmosCommentsRepository commentsRepository)
    {
        _commentsRepository = commentsRepository;
    }

    public async Task<DiagnosticSyncResult> SyncDiagnosticCommentsAsync(
        string reviewId,
        string apiRevisionId,
        string currentDiagnosticsHash,
        CodeDiagnostic[] diagnostics,
        IEnumerable<CommentItemModel> existingComments)
    {
        diagnostics ??= [];

        string newHash = ComputeDiagnosticsHash(diagnostics);

        if (currentDiagnosticsHash == newHash)
        {
            return new DiagnosticSyncResult
            {
                Comments = existingComments
                    .Where(c => c.CommentSource == CommentSource.Diagnostic && c.APIRevisionId == apiRevisionId)
                    .ToList(),
                DiagnosticsHash = newHash,
                WasSynced = false
            };
        }

        Dictionary<string, CommentItemModel> existingDiagnosticComments = existingComments
            .Where(c => c.CommentSource == CommentSource.Diagnostic && c.APIRevisionId == apiRevisionId)
            .ToDictionary(c => c.Id, c => c);

        var result = new List<CommentItemModel>();
        var expectedDiagnosticIds = new HashSet<string>();

        foreach (var diagnostic in diagnostics)
        {
            string diagnosticHash = GenerateDiagnosticHash(diagnostic.TargetId, diagnostic.Text);
            string commentId = $"diag-{apiRevisionId}-{diagnosticHash}";
            string threadId = $"diag-thread-{apiRevisionId}-{diagnosticHash}";
            expectedDiagnosticIds.Add(commentId);

            CommentSeverity newSeverity = MapDiagnosticLevelToSeverity(diagnostic.Level);
            string newCommentText = BuildDiagnosticCommentText(diagnostic.Text, diagnostic.HelpLinkUri);

            if (existingDiagnosticComments.TryGetValue(commentId, out var existingComment))
            {
                bool needsUpdate = false;

                // Re-open if it was auto-resolved by the bot
                if (existingComment.IsResolved)
                {
                    var lastResolveAction = existingComment.ChangeHistory
                        .LastOrDefault(h => h.ChangeAction == CommentChangeAction.Resolved);

                    if (lastResolveAction?.ChangedBy == ApiViewConstants.AzureSdkBotName)
                    {
                        existingComment.IsResolved = false;
                        existingComment.ChangeHistory.Add(new CommentChangeHistoryModel
                        {
                            ChangeAction = CommentChangeAction.UnResolved,
                            ChangedBy = ApiViewConstants.AzureSdkBotName,
                            ChangedOn = DateTime.UtcNow
                        });
                        needsUpdate = true;
                    }
                }

                if (existingComment.Severity != newSeverity)
                {
                    existingComment.Severity = newSeverity;
                    needsUpdate = true;
                }

                if (existingComment.CommentText != newCommentText)
                {
                    existingComment.CommentText = newCommentText;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    await _commentsRepository.UpsertCommentAsync(existingComment);
                }

                result.Add(existingComment);
                continue;
            }

            var comment = new CommentItemModel
            {
                Id = commentId,
                ReviewId = reviewId,
                APIRevisionId = apiRevisionId,
                ElementId = diagnostic.TargetId,
                ThreadId = threadId,
                CommentText = newCommentText,
                CommentSource = CommentSource.Diagnostic,
                Severity = newSeverity,
                CreatedBy = ApiViewConstants.AzureSdkBotName,
                CreatedOn = DateTime.UtcNow,
                IsResolved = false,
                ResolutionLocked = false,
                CommentType = CommentType.APIRevision
            };

            await _commentsRepository.UpsertCommentAsync(comment);
            result.Add(comment);
        }

        // Resolve diagnostics that no longer exist
        foreach (var existingComment in existingDiagnosticComments.Values.Where(existingComment =>
                     !expectedDiagnosticIds.Contains(existingComment.Id) && !existingComment.IsResolved))
        {
            existingComment.IsResolved = true;
            existingComment.ChangeHistory.Add(new CommentChangeHistoryModel
            {
                ChangeAction = CommentChangeAction.Resolved,
                ChangedBy = ApiViewConstants.AzureSdkBotName,
                ChangedOn = DateTime.UtcNow
            });
            await _commentsRepository.UpsertCommentAsync(existingComment);
        }

        return new DiagnosticSyncResult
        {
            Comments = result,
            DiagnosticsHash = newHash,
            WasSynced = true
        };
    }

    private string ComputeDiagnosticsHash(CodeDiagnostic[] diagnostics)
    {
        if (diagnostics == null || diagnostics.Length == 0)
        {
            return string.Empty;
        }

        var sortedDiagnostics = diagnostics
            .OrderBy(d => d.TargetId)
            .ThenBy(d => d.Text)
            .ThenBy(d => d.Level);

        var sb = new StringBuilder();
        foreach (var d in sortedDiagnostics)
        {
            sb.Append(d.TargetId).Append('|').Append(d.Text).Append('|').Append((int)d.Level).Append(';');
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private static string GenerateDiagnosticHash(string targetId, string text)
    {
        string compositeKey = $"{targetId}|{text}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(compositeKey));
        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        return hashString[..16];
    }

    private static string BuildDiagnosticCommentText(string diagnosticText, string helpLinkUri)
    {
        return string.IsNullOrEmpty(helpLinkUri) ? diagnosticText : $"{diagnosticText}\n\n[Details]({helpLinkUri})";
    }

    private static CommentSeverity MapDiagnosticLevelToSeverity(CodeDiagnosticLevel level)
    {
        return level switch
        {
            CodeDiagnosticLevel.Fatal => CommentSeverity.MustFix,
            CodeDiagnosticLevel.Error => CommentSeverity.MustFix,
            CodeDiagnosticLevel.Warning => CommentSeverity.ShouldFix,
            CodeDiagnosticLevel.Info => CommentSeverity.Suggestion,
            _ => CommentSeverity.ShouldFix
        };
    }
}
