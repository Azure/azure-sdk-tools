export function createFeedbackCard() {
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
        },
      },
      {
        type: 'Action.Submit',
        title: '👎Dislike',
        data: {
          action: 'feedback-dislike',
        },
      },
    ],
  };
  return feedbackCard;
}
