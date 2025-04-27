import { feedbackCard } from "./feedback";
import { humanHelpCard } from "./human-help";
import { RAGReply } from "../../backend/rag";
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
        // adaptive card does not support FULL markdown in attachment, use message instead
        body: [],
        actions: [
            {
                type: "Action.ShowCard",
                title: "📑References📑",
                card: referenceListCard,
            },
            {
                type: "Action.ShowCard",
                title: "👍Feedback👎",
                card: feedbackCard,
            },
            {
                type: "Action.ShowCard",
                title: "🕵️‍♂️Human Assistance🕵️‍♀️",
                card: humanHelpCard,
            },
        ],
        $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
        version: "1.6",
    };
}
