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
import { OCRPool } from "../input/image.js";
import { ThinkingHandler } from "../turn/ThinkingHandler.js";

export class FakeModel implements PromptCompletionModel {
    // // OCR
    // private readonly ocrLanguages = ["eng"];
    // private readonly ocrWorkers = 4;
    // private readonly ocrPool = OCRPool.create(
    //     this.ocrLanguages,
    //     this.ocrWorkers
    // );

    // RAG
    private options: RAGOptions;

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
        const thinkingHandler = new ThinkingHandler(context);
        await thinkingHandler.start(context);

        const prompt = await this.generatePromptToRag(context);
        const ragReply = await getRAGReply(prompt, this.options);
        thinkingHandler.stop();

        const card = createReplyCard(ragReply);
        console.log("🚀 ~ RAGModel ~ card:", card);
        const attachment = CardFactory.adaptiveCard(card);
        const replyCard = MessageFactory.attachment(attachment);
        await context.sendActivities([
            MessageFactory.text(ragReply.answer),
            replyCard,
        ]);

        await thinkingHandler.complete();
        return { status: "success" };
    }

    private async generatePromptToRag(context: TurnContext) {
        const removedMentionText = TurnContext.removeRecipientMention(
            context.activity
        );
        const prompt = removedMentionText
            .toLowerCase()
            .replace(/\n|\r/g, "")
            .trim();
        return prompt;
    }
}
