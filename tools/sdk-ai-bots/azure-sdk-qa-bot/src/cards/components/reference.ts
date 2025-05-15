export interface ReferenceData {
  title: string;
  sourceName: string;
  sourceUrl: string;
  excerpt: string;
}
export function createReferenceCard(ref: ReferenceData) {
  return {
    type: 'AdaptiveCard',
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    version: '1.6',
    body: [
      {
        type: 'TextBlock',
        text: ref.title,
        size: 'Large',
        weight: 'Bolder',
        wrap: true,
        style: 'heading',
      },
      {
        type: 'FactSet',
        facts: [
          {
            title: 'Source',
            value: `[${ref.sourceName}](${ref.sourceUrl})`,
          },
          {
            title: 'Excerpt',
            value: ref.excerpt,
          },
        ],
      },
    ],
  };
}
