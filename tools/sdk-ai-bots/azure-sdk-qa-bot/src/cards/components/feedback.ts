export const feedbackCard = {
  type: 'AdaptiveCard',
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  version: '1.6',
  body: [],
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
