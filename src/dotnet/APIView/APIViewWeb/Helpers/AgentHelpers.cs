using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using APIView;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Helpers;

public class AgentHelpers
{

    private static readonly Regex azureSdkAgentTag = new Regex($@"(^|\s)@{Regex.Escape(ApiViewConstants.AzureSdkBotName)}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static Dictionary<string, int> BuildElementIdToLineNumberMapping(RenderedCodeFile codeFile)
    {
        var activeCodeLines = codeFile.CodeFile.GetApiLines(skipDocs: true);
        return activeCodeLines
            .Select((elementId, lineNumber) => new { elementId.lineId, lineNumber })
            .Where(x => !string.IsNullOrEmpty(x.lineId))
            .ToDictionary(x => x.lineId, x => x.lineNumber + 1);
    }

    public static List<ApiViewAgentComment> BuildCommentsForAgent(IEnumerable<CommentItemModel> comments,
        RenderedCodeFile codeFile)
    {
        Dictionary<string, int> elementIdToLineNumber = BuildElementIdToLineNumberMapping(codeFile);

        return (from comment in comments
            where comment.ElementId != null && elementIdToLineNumber.ContainsKey(comment.ElementId)
            select new ApiViewAgentComment
            {
                LineNumber = elementIdToLineNumber[comment.ElementId],
                CreatedOn = comment.CreatedOn,
                Upvotes = comment.Upvotes.Count,
                Downvotes = comment.Downvotes.Count,
                CreatedBy = comment.CreatedBy,
                CommentText = comment.CommentText,
                IsResolved = comment.IsResolved,
                Severity = comment.Severity?.ToString() ?? String.Empty
            }).ToList();
    }

    public static List<ApiViewAgentComment> BuildDiagnosticsForAgent(IEnumerable<CodeDiagnostic> diagnostics,
        RenderedCodeFile codeFile)
    {
        Dictionary<string, int> elementIdToLineNumber = BuildElementIdToLineNumberMapping(codeFile);

        return (from diagnostic in diagnostics
                where diagnostic.TargetId != null && elementIdToLineNumber.ContainsKey(diagnostic.TargetId)
            select new ApiViewAgentComment
            {
                LineNumber = elementIdToLineNumber[diagnostic.TargetId],
                CommentText = diagnostic.Text,
            }).ToList();
    }

    public static string GetCodeLineForElement(RenderedCodeFile codeFile, string elementId)
    {
        var matchingLine = codeFile.CodeFile.GetApiLines(skipDocs: true)
            .FirstOrDefault(line => line.lineId == elementId);
        return matchingLine.lineText ?? string.Empty;
    }

  
    public static bool IsApiViewAgentTagged(CommentItemModel comment, out string commentTextWithIdentifiedTags)
    {
        bool isTagged = azureSdkAgentTag.IsMatch(comment.CommentText);

        commentTextWithIdentifiedTags = azureSdkAgentTag.Replace(
            comment.CommentText,
            m => $"{m.Groups[1].Value}**@{ApiViewConstants.AzureSdkBotName}**"
        );

        return isTagged;
    }
}
