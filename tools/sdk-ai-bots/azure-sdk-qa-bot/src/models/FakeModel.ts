import {
    Memory,
    PromptCompletionModel,
    PromptFunctions,
    PromptTemplate,
    Tokenizer,
} from "@microsoft/teams-ai";
import { CardFactory, MessageFactory, TurnContext } from "botbuilder";
import { getRAGReply, RAGOptions, RAGReply } from "../backend/rag.js";
import { createReplyCard } from "../cards/components/reply.js";
import { PromptResponse } from "@microsoft/teams-ai/lib/types/PromptResponse.js";
import {
    ImageInputProcessor,
    ImageInputProcessorOptions,
} from "../input/ImageInputProcessor.js";
import { ThinkingHandler } from "../turn/ThinkingHandler.js";
import { LinkPromptGenerator } from "../turn/LinkPromptGenerator.js";
import config from "../config.js";

export interface FakeModelOptions {
    rag: RAGOptions;
    input: {
        image: ImageInputProcessorOptions;
    };
}

export class FakeModel implements PromptCompletionModel {
    private imageInputProcessorPromise: Promise<ImageInputProcessor>;
    private options: FakeModelOptions;

    constructor(options: FakeModelOptions) {
        this.options = options;
        const processor = new ImageInputProcessor();
        this.imageInputProcessorPromise = processor.init(options.input.image);
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

        const prompt = await this.generatePrompt(context);
        if (config.debug) {
            await context.sendActivity(`[DEBUG] Prompt: ${prompt}`);
        }
        console.log("🚀 ~ FakeModel ~ completePrompt ~ prompt:", prompt);
        const ragReply = await getRAGReply(prompt, this.options.rag);
        // TODO: try merge cancelTimer and stop into one method
        thinkingHandler.cancelTimer();
        await this.replyToUser(context, ragReply);

        await thinkingHandler.stop();
        return { status: "success" };
    }

    private async replyToUser(context: TurnContext, ragReply: RAGReply) {
        const card = createReplyCard(ragReply);
        console.log("🚀 ~ RAGModel ~ card:", card);
        const attachment = CardFactory.adaptiveCard(card);
        const replyCard = MessageFactory.attachment(attachment);
        await context.sendActivities([
            MessageFactory.text(ragReply.answer),
            replyCard,
        ]);
    }

    private async generatePrompt(context: TurnContext) {
        console.log(
            "🚀 ~ FakeModel ~ generatePrompt ~ context.activity:",
            JSON.stringify(context.activity, null, 2)
        );
        console.log(
            "🚀 ~ FakeModel ~ generatePrompt ~ context.activity.attachments:",
            JSON.stringify(context.activity.attachments, null, 2)
        );
        const removedMentionText = TurnContext.removeRecipientMention(
            context.activity
        );
        const text = removedMentionText
            .toLowerCase()
            .replace(/\n|\r/g, "")
            .trim();
        const textPrompt = `## Question\n${text}\n`;

        const linkPromptGenerator = new LinkPromptGenerator(context);
        const githubLinkPrompts =
            await linkPromptGenerator.generateGithubPullRequestPrompts();

        const prompt = [textPrompt, ...githubLinkPrompts].join("\n\n");
        return prompt;
    }

    // TODO
    private async getImageInputProcessor(): Promise<ImageInputProcessor> {
        const processor = await this.imageInputProcessorPromise;
        return processor;
    }
}
