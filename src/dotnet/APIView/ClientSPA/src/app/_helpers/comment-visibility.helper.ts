import { CommentItemModel, CommentSource, CommentType } from '../_models/commentItemModel';

/**
 * Result of computing visible comments for an active API revision.
 */
export interface VisibleCommentsResult {
  /** All visible comments: user + AI. No display caps applied. */
  allVisibleComments: CommentItemModel[];
  /** User comments (not AI-generated) from any revision. */
  userComments: CommentItemModel[];
  /** AI-generated comments from any revision. */
  aiGeneratedComments: CommentItemModel[];
}

/**
 * Single source of truth for determining which comments are "visible" for a given
 * active API revision. Used by the Conversations panel, badge counts, and quality score.
 *
 * Rules:
 *  1. User comments (not AI-generated): always visible regardless of revision.
 *  2. AI-generated comments: always visible regardless of revision.
 *
 * No display caps are applied here — consumers can apply those on top of the returned result.
 */
export function getVisibleComments(
  comments: CommentItemModel[],
  activeApiRevisionId?: string | null
): VisibleCommentsResult {
  // Only include comments for API revisions, not sample revisions
  const apiRevisionComments = comments.filter(comment =>
    comment.commentType !== CommentType.SampleRevision
  );

  const userComments = apiRevisionComments.filter(comment =>
    comment.commentSource !== CommentSource.AIGenerated
  );

  const aiGeneratedComments = apiRevisionComments.filter(comment =>
    comment.commentSource === CommentSource.AIGenerated
  );

  return {
    allVisibleComments: [...userComments, ...aiGeneratedComments],
    userComments,
    aiGeneratedComments,
  };
}
