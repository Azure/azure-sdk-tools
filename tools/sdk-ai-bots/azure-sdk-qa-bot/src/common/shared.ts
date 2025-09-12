import {
  AzureCliCredential,
  ManagedIdentityCredential,
  TokenCredential,
} from '@azure/identity';
import { logger } from '../logging/logger.js';

const mentionLinkToIgnore = new URL('http://schema.skype.com/Mention');

function normalizeUrl(link: string) {
  return new URL(link).href;
}

export function getUniqueLinks(links: string[]): string[] {
  const set = new Set<string>(links.map(normalizeUrl));
  return Array.from(set).filter((link) => link !== mentionLinkToIgnore.href);
}

export function parseConversationId(id: string): { channelId: string; postId: string | undefined } {
  let postId: string | undefined;
  const parts = id.split(';');
  const channelId = parts[0];
  parts.forEach((part) => {
    if (part.startsWith('messageid=')) {
      postId = part.split('=')[1];
    }
  });
  return { postId, channelId };
}

export async function isAzureAppService(): Promise<boolean> {
  const isLocal = process.env.IS_LOCAL === 'true';
  logger.info('Running in Azure App Service: ' + !isLocal);
  return !isLocal;
}

export async function getAzureCredential(botId: string): Promise<TokenCredential> {
  return (await isAzureAppService()) ? new ManagedIdentityCredential(botId) : new AzureCliCredential();
}
