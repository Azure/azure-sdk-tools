const feedbackReasons = {
  like: ['It solved my question', 'The response gives clear guidance', 'The reference link(s) is helpful', 'Other'],
  dislike: [
    'It misunderstood my question',
    'The response is hard to understand',
    'Out of date/obsolete',
    "The solution doesn't work",
    'The reference link(s) is broken',
    'Other',
  ],
};

function generateToggleId(index: number): string {
  return `reason_${index}`;
}

function createFeedbackActionCard(submitText: string, reasons: string[], action: string) {
  const reasonToggles = reasons.map((reason, index) => ({
    type: 'Input.Toggle',
    title: reason,
    id: generateToggleId(index),
    value: 'false',
  }));
  const question = (action === 'feedback-dislike' ? '👎' : '👍') + 'What is the reason for your feedback?';
  return {
    type: 'AdaptiveCard',
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    version: '1.6',
    body: [
      {
        type: 'TextBlock',
        text: question,
        wrap: true,
      },
      ...reasonToggles,
      {
        type: 'TextBlock',
        text: 'Tell us more.',
        wrap: true,
      },
      {
        type: 'Input.Text',
        id: 'feedbackComment',
        placeholder:
          'Please provide feedback on how we can improve this content. If applicable, provide the first part of the sentence or string at issue.',
        isMultiline: true,
      },
    ],
    actions: [
      {
        type: 'Action.Submit',
        title: submitText,
        data: { action, reasons },
      },
    ],
  };
}

export function createFeedbackCard() {
  const submitLikeCard = {
    type: 'Action.ShowCard',
    title: '👍Yes',
    card: createFeedbackActionCard('Submit👍', feedbackReasons.like, 'feedback-like'),
  };
  const submitDislikeCard = {
    type: 'Action.ShowCard',
    title: '👎No',
    card: createFeedbackActionCard('Submit👎', feedbackReasons.dislike, 'feedback-dislike'),
  };
  const feedbackCard = {
    type: 'AdaptiveCard',
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    version: '1.6',
    body: [
      {
        type: 'TextBlock',
        text: 'Was this conversation helpful to you?',
        wrap: true,
      },
    ],
    actions: [submitLikeCard, submitDislikeCard],
  };
  return feedbackCard;
}

export function extractSelectedReasons(submittedData: any): string[] {
  const selectedReasons: string[] = [];
  const reasons = submittedData.reasons || [];

  reasons.forEach((reason: string, index: number) => {
    const toggleId = generateToggleId(index);
    if (submittedData[toggleId] === 'true') {
      selectedReasons.push(reason);
    }
  });

  return selectedReasons;
}
