using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models;

public class ApiViewAgentComment
{
    [JsonPropertyName("lineNo")]
    public int LineNumber { get; set; }
    /// <summary>
    /// Preview feature: Line identifier. May change or be removed in future versions.
    /// </summary>
    [JsonPropertyName("_lineId")]
    public string LineId { get; set; }
    /// <summary>
    /// Preview feature: Line text content. May change or be removed in future versions.
    /// </summary>
    [JsonPropertyName("_lineText")]
    public string LineText { get; set; }
    [JsonPropertyName("createdOn")]
    public DateTimeOffset CreatedOn { get; set; }
    [JsonPropertyName("upvotes")]
    public int Upvotes { get; set; }
    [JsonPropertyName("downvotes")]
    public int Downvotes { get; set; }
    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; }
    [JsonPropertyName("commentText")]
    public string CommentText { get; set; }
    [JsonPropertyName("isResolved")]
    public bool IsResolved { get; set; }

    [JsonPropertyName("severity")] 
    public string Severity { get; set; }

    [JsonPropertyName("threadId")]
    public string ThreadId { get; set; }
}

public class MentionRequest
{
    [JsonPropertyName("language")]
    public string Language { get; set; }
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; }
    [JsonPropertyName("code")]
    public string Code { get; set; }
    [JsonPropertyName("comments")]
    public List<ApiViewAgentComment> Comments { get; set; }
}

public class AgentChatResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; }

    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; }

    [JsonPropertyName("messages")]
    public List<object> Messages { get; set; }
}
