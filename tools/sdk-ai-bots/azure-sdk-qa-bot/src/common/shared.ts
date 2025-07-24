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
