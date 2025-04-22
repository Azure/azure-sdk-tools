import {
    Memory,
    PromptCompletionModel,
    PromptFunctions,
    PromptTemplate,
    Tokenizer,
} from "@microsoft/teams-ai";
import { PromptResponse } from "@microsoft/teams-ai/lib/types";
import { CardFactory, MessageFactory, TurnContext } from "botbuilder";
import { getRAGReply, RAGOptions } from "../rag/network";
import * as ACData from "adaptivecards-templating";
import { createReplyCardTemplate } from "../cards/card-kit";

export class RAGModel implements PromptCompletionModel {
    private options: RAGOptions;
    private replyCardTemplate: ACData.Template;
    private thinkEmojis = ["⏳", "🤔", "💭", "🧠", "🤩", "🧐", "🚨", "🤭"];
    private thinkingMessage = "⏳Thinking";

    constructor(options: RAGOptions) {
        this.options = options;
        this.replyCardTemplate = createReplyCardTemplate();
    }

    public async completePrompt(
        context: TurnContext,
        memory: Memory,
        functions: PromptFunctions,
        tokenizer: Tokenizer,
        template: PromptTemplate
    ): Promise<PromptResponse<string>> {
        const [timerHandler, id] = await this.showThinkingMessage(context);
        const removedMentionText = TurnContext.removeRecipientMention(
            context.activity
        );
        const txt = removedMentionText
            .toLowerCase()
            .replace(/\n|\r/g, "")
            .trim();
        const createdTime = new Date().toLocaleDateString();
        const ragReply = await getRAGReply(txt, this.options);
        const reply = ragReply.answer;
        // TODO: make it cofigurable
        const icmUrl =
            "https://portal.microsofticm.com/imp/v3/incidents/create";
        const card = this.replyCardTemplate.expand({
            $root: { createdTime, reply, icmUrl },
        });
        const attachment = CardFactory.adaptiveCard(card);
        const replyCard = MessageFactory.attachment(attachment);
        replyCard.id = id;
        await context.updateActivity(replyCard);
        if (timerHandler) clearTimeout(timerHandler);
        return {
            status: "success",
        };
    }

    private async showThinkingMessage(
        context: TurnContext
    ): Promise<[NodeJS.Timeout, string]> {
        const resource = await context.sendActivity(this.thinkingMessage);
        const timerHandler = setInterval(async () => {
            const updated: Partial<TurnContext> = {
                type: "message",
                id: resource.id,
                text: this.updateThinkingMessage(),
                conversation: context.activity.conversation,
            } as any;
            await context.updateActivity(updated);
        }, 500);
        return [timerHandler, resource.id];
    }

    private updateThinkingMessage() {
        const index = getRandomInt(0, this.thinkEmojis.length - 1);
        const start = this.thinkingMessage.indexOf("Thi");
        this.thinkingMessage =
            this.thinkEmojis[index] +
            this.thinkingMessage.substring(start) +
            ".";
        return this.thinkingMessage;

        function getRandomInt(min: number, max: number) {
            min = Math.ceil(min);
            max = Math.floor(max);
            return Math.floor(Math.random() * (max - min + 1)) + min;
        }
    }
}
