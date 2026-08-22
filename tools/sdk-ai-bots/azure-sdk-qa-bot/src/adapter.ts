import { TeamsAdapter } from '@microsoft/teams-ai';
import {
  JwtTokenProviderFactory,
  ManagedIdentityServiceClientCredentialsFactory,
} from 'botframework-connector';

// This bot's main dialog.
import config from './config/config.js';
import { LogMiddleware } from './middleware/LogMiddleware.js';
import { logger } from './logging/logger.js';
import { getTurnContextLogMeta } from './logging/utils.js';
import { isAzureAppService } from './common/shared.js';
import { sendActivityWithRetry } from './activityUtils.js';

// Bot Framework Service audience. When the Logic App fronting this bot POSTs
// to /api/messages it must acquire an AAD token with this audience (AAD refuses
// to mint tokens whose audience is a User-Assigned Managed Identity SP —
// AADSTS100040), so the audience of the incoming JWT is this URL rather than
// the bot's UAMI clientId.
const BOT_FRAMEWORK_SERVICE_AUDIENCE = 'https://api.botframework.com';

/**
 * `ManagedIdentityServiceClientCredentialsFactory.createCredentials(appId, …)`
 * throws "Invalid Managed ID" when `appId !== this.appId`. In UserAssignedMsi
 * mode, `getAppId(claimsIdentity)` in botframework-connector returns the JWT's
 * audience claim, which for Logic-App-initiated calls is
 * `https://api.botframework.com`, not the UAMI clientId — so the default
 * validation always rejects those calls with HTTP 500 even though the token
 * itself is legitimate. Accept the Bot Framework Service audience as an alias
 * for `this.appId`; the base class still constructs the outbound
 * `ManagedIdentityAppCredentials` from `this.appId`, so the reply token is
 * unchanged.
 */
class BotFrameworkAudienceAwareManagedIdentityFactory extends ManagedIdentityServiceClientCredentialsFactory {
  async isValidAppId(appId: string): Promise<boolean> {
    if (appId === BOT_FRAMEWORK_SERVICE_AUDIENCE) {
      return true;
    }
    return super.isValidAppId(appId);
  }
}

// For Teams App Test Tool, don't require authentication
const adapterConfig = config.isLocal && !config.MicrosoftAppId
  ? {} // No authentication for test tool
  : config;

const isUserAssignedMsi =
  (config.MicrosoftAppType ?? '').trim().toLowerCase() === 'userassignedmsi';
const credentialsFactory =
  isUserAssignedMsi && config.MicrosoftAppId
    ? new BotFrameworkAudienceAwareManagedIdentityFactory(
        config.MicrosoftAppId,
        new JwtTokenProviderFactory(),
      )
    : undefined;

const adapter = new TeamsAdapter(adapterConfig, credentialsFactory);
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
