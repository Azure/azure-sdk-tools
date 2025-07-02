using System.Collections.Generic;
using System.Linq;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Helpers;

public class AgentHelpers
{
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
}
