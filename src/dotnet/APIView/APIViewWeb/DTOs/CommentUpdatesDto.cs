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
        public CommentThreadUpdateAction CommentThreadUpdateAction { get; set; }
        public string NodeId { get; set; }
        public string NodeIdHashed { get; set; }
        public string ReviewId { get; set; }
        public string RevisionId { get; set; }
        public string CommentId { get; set; }
        public string ElementId { get; set; }
        public string CommentText { get; set; }
        public CommentItemModel Comment { get; set; }
        public string ResolvedBy { get; set; }
        public int? AssociatedRowPositionInGroup { get; set; }
        public bool? AllowAnyOneToResolve { get; set; }
    }
}
