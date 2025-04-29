import {
    Memory,
    PromptCompletionModel,
    PromptFunctions,
    PromptTemplate,
    Tokenizer,
} from "@microsoft/teams-ai";
import { CardFactory, MessageFactory, TurnContext } from "botbuilder";
import { getRAGReply, RAGOptions } from "../backend/rag.js";
import { createReplyCard } from "../cards/components/reply.js";
import { PromptResponse } from "@microsoft/teams-ai/lib/types/PromptResponse.js";

interface MessageStatus {
    gotReply: boolean;
}

export class RAGModel implements PromptCompletionModel {
    private options: RAGOptions;
    private thinkEmojis = ["⏳", "🤔", "💭", "🧠", "🤩", "🧐", "🚨", "🤭"];
    private defaultThinkingMessage = "⏳Thinking";
    private thinkingMessage = "⏳Thinking";

    constructor(options: RAGOptions) {
        this.options = options;
    }

    public async completePrompt(
        context: TurnContext,
        memory: Memory,
        functions: PromptFunctions,
        tokenizer: Tokenizer,
        template: PromptTemplate
    ): Promise<PromptResponse<string>> {
        const messageStatus: MessageStatus = { gotReply: false };
        const [timerHandler, id] = await this.showThinkingMessage(
            context,
            messageStatus
        );
        const removedMentionText = TurnContext.removeRecipientMention(
            context.activity
        );
        const txt = removedMentionText
            .toLowerCase()
            .replace(/\n|\r/g, "")
            .trim();
        const ragReply = await getRAGReply(txt, this.options);
        messageStatus.gotReply = true;
        const card = createReplyCard(ragReply);
        console.log("🚀 ~ RAGModel ~ card:", card);
        const attachment = CardFactory.adaptiveCard(card);
        const replyCard = MessageFactory.attachment(attachment);
        if (timerHandler) {
            clearInterval(timerHandler);
        }
        // TODO: refactor
        const updated: Partial<TurnContext> = {
            type: "message",
            id,
            text: "✅Thinking complete",
            conversation: context.activity.conversation,
        } as any;
        await context.sendActivities([
            MessageFactory.text(ragReply.answer),
            replyCard,
        ]);
        await context.updateActivity(updated);
        this.thinkingMessage = this.defaultThinkingMessage;
        return { status: "success" };
    }

    private async showThinkingMessage(
        context: TurnContext,
        messageStatus: MessageStatus
    ): Promise<[NodeJS.Timeout, string]> {
        const resource = await context.sendActivity(this.thinkingMessage);
        const timerHandler = setInterval(async () => {
            const updated: Partial<TurnContext> = {
                type: "message",
                id: resource.id,
                text: this.updateThinkingMessage(),
                conversation: context.activity.conversation,
            } as any;
            if (!messageStatus.gotReply) {
                await context.updateActivity(updated);
            }
        }, 1000);
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
