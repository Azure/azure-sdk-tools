import { describe, expect, it } from 'vitest';
import type { Mention } from 'botbuilder';
import { getTeamsMentionEntities, type TeamsMention } from '../src/common/mentions.js';

const mention: TeamsMention = {
    text: '<at>linked.person@microsoft.com</at>',
    mentioned: {
        id: '11111111-2222-3333-4444-555555555555',
        name: 'Linked Person',
    },
};
const entity: Mention = { ...mention, type: 'mention' };

describe('getTeamsMentionEntities', () => {
    it('attaches only exact, explicit AT markup and collapses duplicate entities', () => {
        expect(getTeamsMentionEntities(`Please ping ${mention.text}, then ${mention.text}.`, [
            mention, mention, { ...mention, text: '<at>unused@microsoft.com</at>' },
        ]        )).toEqual([entity]);
    });

    it('does not infer entities from GitHub handles, plain UPNs, or unknown tags', () => {
        expect(getTeamsMentionEntities(
            '@linked-user linked.person@microsoft.com <at>unknown@microsoft.com</at>',
            [mention]
        )).toEqual([]);
    });

    it('is generic across Entra, UPN, and Teams account identifiers', () => {
        for (const id of [mention.mentioned.id, 'linked.person@microsoft.com', '29:teams-user']) {
            const candidate = { ...mention, mentioned: { id, name: 'Linked Person' } };
            expect(getTeamsMentionEntities(mention.text, [candidate])).toEqual([
                { ...candidate, type: 'mention' },
            ]);
        }
    });

    it('distinguishes people with identical display names using their explicit markup', () => {
        const other = {
            ...mention,
            text: '<at>other.person@microsoft.com</at>',
            mentioned: { id: '22222222-2222-3333-4444-555555555555', name: 'Linked Person' },
        };
        expect(getTeamsMentionEntities(`${mention.text} ${other.text}`, [mention, other])).toEqual([
            entity, { ...other, type: 'mention' },
        ]);
    });

    it('accepts absent or null mention metadata', () => {
        for (const mentions of [undefined, null, []]) {
            expect(getTeamsMentionEntities(mention.text, mentions)).toEqual([]);
        }
    });

    it('rejects ambiguous markup instead of targeting the wrong account', () => {
        const other = { ...mention, mentioned: { id: 'another-user', name: 'Another Person' } };
        expect(() => getTeamsMentionEntities(mention.text, [mention, other])).toThrow(
            'Different Teams users cannot share the same mention text.'
        );
    });

    it('surfaces invalid mention metadata', () => {
        const invalid = { ...mention, mentioned: { id: '', name: 'Linked Person' } };
        expect(() => getTeamsMentionEntities(mention.text, [invalid])).toThrow(
            'Teams mentions require non-empty text and a user ID.'
        );
    });
});
