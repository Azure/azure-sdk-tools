import type { Mention } from 'botbuilder';

export type TeamsMention = Pick<Mention, 'text' | 'mentioned'>;

export function getTeamsMentionEntities(
    message: string,
    mentions: TeamsMention[] | null = []
): Mention[] {
    const entities = new Map<string, Mention>();
    for (const mention of mentions ?? []) {
        if (!mention.text || !mention.mentioned.id) {
            throw new Error('Teams mentions require non-empty text and a user ID.');
        }
        if (!message.includes(mention.text)) continue;
        const existing = entities.get(mention.text);
        if (existing && existing.mentioned.id !== mention.mentioned.id) {
            throw new Error('Different Teams users cannot share the same mention text.');
        }
        entities.set(mention.text, { ...mention, type: 'mention' });
    }
    return [...entities.values()];
}
