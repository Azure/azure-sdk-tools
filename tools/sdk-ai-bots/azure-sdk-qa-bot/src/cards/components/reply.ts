import { feedbackCard } from "./feedback";
import { RAGReply } from "../../backend/rag";
import { createReferencesListCard } from "./reference-list";
import { supportChannelCard } from "./support-channel";

export function createReplyCard(reply: RAGReply) {
    const referenceDataList = reply.references.map((ref) => ({
        title: ref.title,
        sourceName: ref.link.split("/").pop() || "",
        sourceUrl: ref.link,
        excerpt: ref.content,
    }));
    const referenceListCard = createReferencesListCard(referenceDataList);
    const referenceAction = {
        type: "Action.ShowCard",
        title: "📑References📑",
        card: referenceListCard,
    };
    const feedbackAction = {
        type: "Action.ShowCard",
        title: "👍Feedback👎",
        card: feedbackCard,
    };
    const supportChannelAction = {
        type: "Action.ShowCard",
        title: "🕵️‍♂️Support Channels🕵️‍♀️",
        card: supportChannelCard,
    };
    const actions =
        referenceDataList.length > 0
            ? [referenceAction, feedbackAction, supportChannelAction]
            : [feedbackAction, supportChannelAction];
    const card = {
        type: "AdaptiveCard",
        // adaptive card does not support FULL markdown in attachment, use message instead
        body: [],
        actions,
        $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
        version: "1.6",
    };
    return card;
}
