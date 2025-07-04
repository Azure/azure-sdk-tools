using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Helpers;

public class AgentHelpers
{

    private static readonly Regex azureSdkAgentTag = new Regex($@"(^|\s)@{Regex.Escape(ApiViewConstants.AzureSdkBotName)}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ApiViewComment> BuildCommentsForAgent(IEnumerable<CommentItemModel> comments,
        RenderedCodeFile codeFile)
    {
        var activeCodeLines = codeFile.CodeFile.GetApiLines(skipDocs: true);
        
        Dictionary<string, int> elementIdToLineNumber = activeCodeLines
            .Select((elementId, lineNumber) => new { elementId.lineId, lineNumber })
            .Where(x => !string.IsNullOrEmpty(x.lineId))
            .ToDictionary(x => x.lineId, x => x.lineNumber + 1);

        List<ApiViewComment> commentsForAgent = comments
            .Select(threadComment => new ApiViewComment
            {
                LineNumber = elementIdToLineNumber.TryGetValue(threadComment.ElementId, out int id) ? id : -1,
                CreatedOn = threadComment.CreatedOn,
                Upvotes = threadComment.Upvotes.Count,
                Downvotes = threadComment.Downvotes.Count,
                CreatedBy = threadComment.CreatedBy,
                CommentText = threadComment.CommentText,
                IsResolved = threadComment.IsResolved
            })
            .ToList();

        return commentsForAgent;
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
