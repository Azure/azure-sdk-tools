export function createFeedbackCard(conversationId: string, activityId: string) {
  const feedbackCard = {
    type: 'AdaptiveCard',
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    version: '1.6',
    body: [
      {
        type: 'Input.Text',
        id: 'feedbackComment',
        placeholder: 'Please provide your feedback here...',
        isMultiline: true,
      },
    ],
    actions: [
      {
        type: 'Action.Submit',
        title: '👍Like',
        data: {
          action: 'feedback-like',
          conversationId,
          activityId,
        },
      },
      {
        type: 'Action.Submit',
        title: '👎Dislike',
        data: {
          action: 'feedback-dislike',
          conversationId,
          activityId,
        },
      },
    ],
  };
  return feedbackCard;
}
