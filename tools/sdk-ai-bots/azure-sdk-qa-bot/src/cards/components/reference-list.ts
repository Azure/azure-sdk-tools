import { createReferenceCard, ReferenceData } from "./reference.js";

function createReferenceWraper(title: string, referenceData: ReferenceData) {
    return {
        type: "Action.ShowCard",
        title: title,
        card: createReferenceCard(referenceData),
    };
}

export function createReferencesListCard(references: ReferenceData[]) {
    return {
        $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
        type: "AdaptiveCard",
        version: "1.6",
        body: [
            {
                type: "TextBlock",
                text: "References",
                size: "Medium",
                weight: "Bolder",
                wrap: true,
                style: "heading",
            },
        ],
        actions: references.map((reference) =>
            createReferenceWraper(reference.title, reference)
        ),
    };
}
