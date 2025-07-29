import { TurnContext, TeamsChannelData } from 'botbuilder';

export function getTurnContextLogMeta(context: TurnContext) {
  const { activity } = context;
  const channelData = activity.channelData as TeamsChannelData;
  const channelName = channelData?.channel?.name ?? 'not-found';

  return {
    activityId: activity.id,
    convId: activity.conversation.id,
    userId: activity.from.id,
    timestamp: activity.timestamp,
    type: activity.type,
    channelName,
  };
}
