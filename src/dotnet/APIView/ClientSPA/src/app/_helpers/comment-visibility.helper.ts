import { CommentItemModel, CommentSource } from '../_models/commentItemModel';

/**
 * Result of computing visible comments for an active API revision.
 */
export interface VisibleCommentsResult {
  /** All visible comments: user + AI + diagnostics for active revision. No display caps applied. */
  allVisibleComments: CommentItemModel[];
  /** User comments (not diagnostic, not AI-generated) from any revision. */
  userComments: CommentItemModel[];
  /** AI-generated comments from any revision. */
  aiGeneratedComments: CommentItemModel[];
  /** All diagnostic comments for the active revision (no cap). */
  diagnosticCommentsForRevision: CommentItemModel[];
}

/**
 * Single source of truth for determining which comments are "visible" for a given
 * active API revision. Used by the Conversations panel, badge counts, and quality score.
 *
 * Rules:
 *  1. User comments (not diagnostic, not AI-generated): always visible regardless of revision.
 *  2. AI-generated comments: always visible regardless of revision.
 *  3. Diagnostic comments: only visible if they belong to the active revision.
 *
 * No display caps (e.g., the 250-diagnostic limit in the Conversations panel) are applied
 * here — consumers can apply those on top of the returned result.
 */
export function getVisibleComments(
  comments: CommentItemModel[],
  activeApiRevisionId: string | null
): VisibleCommentsResult {
  const userComments = comments.filter(comment =>
    comment.commentSource !== CommentSource.Diagnostic && comment.commentSource !== CommentSource.AIGenerated
  );

  const aiGeneratedComments = comments.filter(comment =>
    comment.commentSource === CommentSource.AIGenerated
  );

  const diagnosticCommentsForRevision = comments.filter(comment =>
    comment.commentSource === CommentSource.Diagnostic && comment.apiRevisionId === activeApiRevisionId
  );

  return {
    allVisibleComments: [...userComments, ...aiGeneratedComments, ...diagnosticCommentsForRevision],
    userComments,
    aiGeneratedComments,
    diagnosticCommentsForRevision,
  };
}
