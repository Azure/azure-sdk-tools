import channelConfigManager from './channel.js';

export async function getRagTanent(channelId: string): Promise<string> {
  try {
    const channelConfig = await channelConfigManager.getChannelConfig(channelId);
    return channelConfig.tenant;
  } catch (error) {
    console.error('Failed to get RAG tenant for channel:', channelId, error);
    // Fallback to default configuration
    const config = await channelConfigManager.getConfig();
    return config.default.tenant;
  }
}

export async function getRagEndpoint(channelId: string): Promise<string> {
  try {
    const channelConfig = await channelConfigManager.getChannelConfig(channelId);
    return channelConfig.endpoint;
  } catch (error) {
    console.error('Failed to get RAG endpoint for channel:', channelId, error);
    // Fallback to default configuration
    const config = await channelConfigManager.getConfig();
    return config.default.endpoint;
  }
}
