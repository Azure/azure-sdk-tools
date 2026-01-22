import { createFeedbackCard } from './feedback.js';
import { supportChannelCard } from './support-channel.js';

export function createContactCard() {
  const feedbackCard = createFeedbackCard();
  const feedbackAction = {
    type: 'Action.ShowCard',
    title: '👍Feedback👎',
    card: feedbackCard,
  };
  const supportChannelAction = {
    type: 'Action.ShowCard',
    title: '🕵️‍♂️Support Channels🕵️‍♀️',
    card: supportChannelCard,
  };
  const actions = [feedbackAction, supportChannelAction];
  const card = {
    type: 'AdaptiveCard',
    // adaptive card does not support FULL markdown in attachment, use message instead
    body: [
      {
        type: 'TextBlock',
        text: '🤖 AI-generated response. Please verify before taking action.',
        wrap: true,
      },
      {
        type: 'TextBlock',
        text: '📝 Please note that the bot is unable to reply to edited messages.',
        wrap: true,
      },
    ],
    actions,
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    // Currently Microsoft Teams doesn't fully support version 1.6, use 1.5 to ensure compatibility
    version: '1.5',
  };
  return card;
}
