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
        text: '🤖 The following is AI-generated response. Please review before applying to production.',
        wrap: true,
      },
      {
        type: 'TextBlock',
        text: '📝 The bot will automatically follow up with the original poster. Once others join the thread, everyone must @Azure SDK QA bot for follow-up; message edits won\'t trigger a reply.',
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
