import { TeamsAdapter } from '@microsoft/teams-ai';

// This bot's main dialog.
import config from './config/config.js';
import { LogMiddleware } from './middleware/LogMiddleware.js';
import { logger } from './logging/logger.js';
import { getTurnContextLogMeta } from './logging/utils.js';
import { isAzureAppService } from './common/shared.js';
import { sendActivityWithRetry } from './activityUtils.js';

// For Teams App Test Tool, don't require authentication
const adapterConfig = config.isLocal && !config.MicrosoftAppId
  ? {} // No authentication for test tool
  : config;

const adapter = new TeamsAdapter(adapterConfig);
adapter.use(new LogMiddleware());

// Catch-all for errors.
const onTurnErrorHandler = async (context, error) => {
  // This check writes out errors to console log .vs. app insights.
  // NOTE: In production environment, you should consider logging this to Azure
  //       application insights.
  const meta = getTurnContextLogMeta(context);
  const stack = 'stack' in error ? (error.stack as string).replace(/\n/g, ' ') : '';
  logger.error(`\n [onTurnError] unhandled error: ${error}, call stack: ${stack}`, { meta });

  // Only send error message for user messages, not for other message types so the bot doesn't spam a channel or chat.
  if (context.activity.type === 'message') {
    // Send a trace activity, which will be displayed in Bot Framework Emulator
    await context.sendTraceActivity(
      'OnTurnError Trace',
      `${error}`,
      'https://www.botframework.com/schemas/error',
      'TurnError'
    );

    if (
      error &&
      'message' in error &&
      typeof error.message === 'string' &&
      !(error.message as string).includes("Cannot read properties of null (reading 'role')")
    ) {
      // Send a message to the user
      const errorMessage =
        `The bot encountered an error or bug.` +
        (!(await isAzureAppService()) ? `\nError: ${error}. Stack: ${(error as Error)?.stack}` : '');
      await sendActivityWithRetry(context, errorMessage);
      logger.warn(`Sent error to teams. Error: ${error}`, { meta });
    }
  }
};

// Set the onTurnError for the singleton TeamsAdapter.
adapter.onTurnError = onTurnErrorHandler;

export default adapter;
