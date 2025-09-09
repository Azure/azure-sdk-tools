import { TurnContext, Middleware } from 'botbuilder';
import { logger } from '../logging/logger.js';
import { getTurnContextLogMeta } from '../logging/utils.js';

export class LogMiddleware implements Middleware {
  async onTurn(context: TurnContext, next: () => Promise<void>): Promise<void> {
    const { activity } = context;
    const meta = getTurnContextLogMeta(context);
    logger.info('Incoming activity', { meta });

    await next();

    logger.info('Turn processing completed', { convId: activity.conversation.id, meta });
  }
}
