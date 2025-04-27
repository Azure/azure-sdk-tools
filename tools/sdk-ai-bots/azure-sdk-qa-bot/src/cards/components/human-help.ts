import { supportChannelCard } from "./support-channel";
import config from "../../config";

export const humanHelpCard = {
    type: "AdaptiveCard",
    body: [],
    actions: [
        {
            type: "Action.ShowCard",
            title: "Support Channels",
            card: supportChannelCard,
        },
    ],
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    version: "1.6",
};
