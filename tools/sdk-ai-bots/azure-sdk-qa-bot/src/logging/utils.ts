import { TurnContext } from 'botbuilder';

export function getTurnContextLogMeta(context: TurnContext) {
  const { activity } = context;

  return {
    activityId: activity.id,
    convId: activity.conversation.id,
    userId: activity.from.id,
    timestamp: activity.timestamp,
    type: activity.type,
  };
}
