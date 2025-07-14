import { TurnContext } from 'botbuilder';
import { getTurnContextLogMeta } from '../logging/utils.js';

export class ThinkingHandler {
  private readonly thinkEmojis = ['⏳', '🤔', '💭', '🧠', '🤩', '🧐', '🚨', '🤭'];
  private readonly defaultThinkingMessage = '⏳Thinking';

  private readonly context: TurnContext;
  private thinkingMessage = this.defaultThinkingMessage;
  private isThinking = true;
  private timerHandler: NodeJS.Timeout | undefined = undefined;
  private resourceId: string | undefined = undefined;
  private logMeta: object;
  constructor(context: TurnContext) {
    this.context = context;
    this.logMeta = getTurnContextLogMeta(context);
  }

  public async start(context: TurnContext): Promise<void> {
    const resource = await context.sendActivity(this.thinkingMessage);
    this.timerHandler = setInterval(async () => {
      const updated: Partial<TurnContext> = {
        type: 'message',
        id: resource.id,
        text: this.updateThinkingMessage(),
        conversation: context.activity.conversation,
      } as any;
      if (this.isThinking) {
        await context.updateActivity(updated);
      }
    }, 1000);
    console.log('🚀 ~ ThinkingHandler ~ this.timerHandler=setInterval ~ resource.id:', resource.id);
    this.resourceId = resource.id;
  }

  // cancel timer as soon as possible when get reply
  public cancelTimer() {
    this.isThinking = false;
    if (this.timerHandler) clearInterval(this.timerHandler);
  }

  // separate this method from cancelTimer to make sure complete message is always shown
  public async stop(answer: string) {
    const updated: Partial<TurnContext> = {
      type: 'message',
      id: this.resourceId,
      text: answer,
      conversation: this.context.activity.conversation,
    } as any;
    await this.context.updateActivity(updated);
  }

  private updateThinkingMessage() {
    const index = getRandomInt(0, this.thinkEmojis.length - 1);
    const start = this.thinkingMessage.indexOf('Thi');
    this.thinkingMessage = this.thinkEmojis[index] + this.thinkingMessage.substring(start) + '.';
    return this.thinkingMessage;

    function getRandomInt(min: number, max: number) {
      min = Math.ceil(min);
      max = Math.floor(max);
      return Math.floor(Math.random() * (max - min + 1)) + min;
    }
  }
}
