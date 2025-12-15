/**
 * Feedback reasons for AI-generated comments
 * Used when users downvote or provide feedback on AI comments
 */
export enum AICommentFeedbackReason {
  FactuallyIncorrect = 'FactuallyIncorrect',
  RenderingBug = 'RenderingBug',
  AcceptedRenderingChoice = 'AcceptedRenderingChoice',
  AcceptedSDKPattern = 'AcceptedSDKPattern',
  OutdatedGuideline = 'OutdatedGuideline'
}

/**
 * Display labels for feedback reasons
 */
export const AI_COMMENT_FEEDBACK_REASON_LABELS: Record<AICommentFeedbackReason, string> = {
  [AICommentFeedbackReason.FactuallyIncorrect]: 'This comment is factually incorrect',
  [AICommentFeedbackReason.RenderingBug]: 'This is an APIView rendering bug',
  [AICommentFeedbackReason.AcceptedRenderingChoice]: 'This is an accepted APIView rendering choice',
  [AICommentFeedbackReason.AcceptedSDKPattern]: 'This is an accepted SDK design pattern',
  [AICommentFeedbackReason.OutdatedGuideline]: 'The guideline cited here is out-of-date'
};

export const AI_COMMENT_FEEDBACK_REASONS = Object.values(AICommentFeedbackReason).map(key => ({
  key,
  label: AI_COMMENT_FEEDBACK_REASON_LABELS[key]
}));

