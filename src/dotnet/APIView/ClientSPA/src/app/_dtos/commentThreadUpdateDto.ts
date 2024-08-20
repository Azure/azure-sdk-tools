import { CommentItemModel } from "../_models/commentItemModel";

export enum CommentThreadUpdateAction {
    CommentCreated = 0,
    CommentTextUpdate,
    CommentResolved,
    CommentUnResolved,
    CommentUpVoted,
    CommentDeleted
}

export interface CommentUpdateDto {
    CommentThreadUpdateAction: CommentThreadUpdateAction;
    reviewId?: string;
    commentId?: string;
    elementId?: string;
    commentText?: string;
    comment?: CommentItemModel
}