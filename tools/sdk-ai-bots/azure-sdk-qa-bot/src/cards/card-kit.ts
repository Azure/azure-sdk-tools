import replyCard from "./reply.json";
import feedbackCard from "./feedback.json";
import supportChannelCard from "./support-channel.json";
import humanHelpCard from "./human-help.json";
import * as ACData from "adaptivecards-templating";

export function createReplyCardTemplate() {
    humanHelpCard.actions[0].card = supportChannelCard;
    replyCard.actions[0].card = feedbackCard;
    replyCard.actions[1].card = humanHelpCard;
    const replyCardTemplate = new ACData.Template(replyCard);
    return replyCardTemplate;
}
