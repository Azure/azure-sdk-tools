import { TurnContext } from "botbuilder";

export class ThinkingHandler {
    private readonly thinkEmojis = [
        "⏳",
        "🤔",
        "💭",
        "🧠",
        "🤩",
        "🧐",
        "🚨",
        "🤭",
    ];
    private readonly defaultThinkingMessage = "⏳Thinking";

    private readonly context: TurnContext;
    private thinkingMessage = this.defaultThinkingMessage;
    private isThinking = true;
    private timerHandler: NodeJS.Timeout | undefined = undefined;
    private messageId: string | undefined = undefined;

    constructor(context: TurnContext) {
        this.context = context;
    }

    public async start(context: TurnContext): Promise<void> {
        const resource = await context.sendActivity(this.thinkingMessage);
        this.timerHandler = setInterval(async () => {
            const updated: Partial<TurnContext> = {
                type: "message",
                id: resource.id,
                text: this.updateThinkingMessage(),
                conversation: context.activity.conversation,
            } as any;
            if (this.isThinking) {
                await context.updateActivity(updated);
            }
        }, 1000);
        this.messageId = resource.id;
    }

    public async stop() {
        this.isThinking = false;
        if (this.timerHandler) clearInterval(this.timerHandler);
    }

    // separate this method from stop to make sure complete message is always shown,
    // since complete message may be later than stop message if sending immediately after timer get cancelled
    public async complete() {
        const updated: Partial<TurnContext> = {
            type: "message",
            id: this.messageId,
            text: "✅Thinking complete",
            conversation: this.context.activity.conversation,
        } as any;
        await this.context.updateActivity(updated);
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
