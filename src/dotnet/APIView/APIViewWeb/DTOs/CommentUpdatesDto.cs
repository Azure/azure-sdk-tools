using System.Text.Json.Serialization;
using APIViewWeb.LeanModels;

namespace APIViewWeb.DTOs
{
    public enum CommentThreadUpdateAction
    {
        CommentCreated = 0,
        CommentTextUpdate,
        CommentResolved,
        CommentUnResolved,
        CommentUpVoteToggled,
        CommentDeleted
    }

    public class CommentUpdatesDto
    {
        [JsonPropertyName("commentThreadUpdateAction")]
        public CommentThreadUpdateAction CommentThreadUpdateAction { get; set; }
        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }
        [JsonPropertyName("nodeIdHashed")]
        public string NodeIdHashed { get; set; }
        [JsonPropertyName("reviewId")]
        public string ReviewId { get; set; }
        [JsonPropertyName("revisionId")]
        public string RevisionId { get; set; }
        [JsonPropertyName("commentId")]
        public string CommentId { get; set; }
        [JsonPropertyName("elementId")]
        public string ElementId { get; set; }
        [JsonPropertyName("commentText")]
        public string CommentText { get; set; }
        [JsonPropertyName("comment")]
        public CommentItemModel Comment { get; set; }
        [JsonPropertyName("resolvedBy")]
        public string ResolvedBy { get; set; }
        [JsonPropertyName("associatedRowPositionInGroup")]
        public int? AssociatedRowPositionInGroup { get; set; }
        [JsonPropertyName("allowAnyOneToResolve")]
        public bool? AllowAnyOneToResolve { get; set; }
    }
}
