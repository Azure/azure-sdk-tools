import { CommentItemModel } from "../_models/commentItemModel";

export enum CommentThreadUpdateAction {
    CommentCreated = 0,
    CommentTextUpdate,
    CommentResolved,
    CommentUnResolved,
    CommentUpVoteToggled,
    CommentDeleted
}

export interface CommentUpdatesDto {
    commentThreadUpdateAction: CommentThreadUpdateAction;
    nodeId?: string; // effectively the same as the element id
    nodeIdHashed?: string;
    reviewId: string;
    revisionId?: string; // revision ids are used in conversation page to group comments
    commentId?: string;
    elementId?: string;
    commentText?: string;
    comment?: CommentItemModel;
    resolvedBy?: string;    
    associatedRowPositionInGroup?: number;
    allowAnyOneToResolve?: boolean;
    title: string;
}