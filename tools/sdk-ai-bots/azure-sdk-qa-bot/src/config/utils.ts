import { channelToRagTanent } from './config.js';

export function getRagTanent(channelId: string) {
  const tanentId = channelId in channelToRagTanent ? channelToRagTanent[channelId] : channelToRagTanent.default;
  return tanentId;
}
