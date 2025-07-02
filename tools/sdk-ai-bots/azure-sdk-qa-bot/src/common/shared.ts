const mentionLinkToIgnore = new URL('http://schema.skype.com/Mention');

function normalizeUrl(link: string) {
  return new URL(link).href;
}

export function getUniqueLinks(links: string[]): string[] {
  const set = new Set<string>(links.map(normalizeUrl));
  return Array.from(set).filter((link) => link !== mentionLinkToIgnore.href);
}
