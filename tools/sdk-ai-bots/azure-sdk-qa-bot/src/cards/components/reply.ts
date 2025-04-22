import { feedbackCard } from "./feedback";
import { humanHelpCard } from "./human-help";
import { RAGReply } from "../../rag/network";
import { createReferencesListCard } from "./reference-list";

export function createReplyCard(reply: RAGReply) {
    const referenceDataList = reply.references.map((ref) => ({
        title: ref.title,
        sourceName: ref.link.split("/").pop() || "",
        sourceUrl: ref.link,
        excerpt: ref.content,
    }));
    const referenceListCard = createReferencesListCard(referenceDataList);
    return {
        type: "AdaptiveCard",
        body: [
            {
                type: "TextBlock",
                text: reply.answer,
                wrap: true,
            },
            {
                type: "ActionSet",
                actions: [
                    {
                        type: "Action.ShowCard",
                        title: "📑References📑",
                        card: referenceListCard,
                    },
                ],
            },
        ],
        actions: [
            {
                type: "Action.ShowCard",
                title: "👍Feedback👎",
                card: feedbackCard,
            },
            {
                type: "Action.ShowCard",
                title: "🕵️‍♂️Human Help🕵️‍♀️",
                card: humanHelpCard,
            },
        ],
        $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
        version: "1.6",
    };
}
