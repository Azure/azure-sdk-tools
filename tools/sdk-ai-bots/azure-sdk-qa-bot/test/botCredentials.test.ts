import { describe, expect, it } from 'vitest';

import {
  BOT_FRAMEWORK_SERVICE_AUDIENCE,
  BotFrameworkAudienceAwareManagedIdentityFactory,
  createBotCredentialsFactory,
} from '../src/auth/botCredentials.js';

describe('bot credential selection', () => {
  it('uses the bot managed identity directly', () => {
    const factory = createBotCredentialsFactory({
      appId: 'managed-identity-client-id',
      appType: 'UserAssignedMsi',
    });

    expect(factory).toBeInstanceOf(BotFrameworkAudienceAwareManagedIdentityFactory);
  });

  it('accepts the Bot Framework service audience', async () => {
    const factory = createBotCredentialsFactory({
      appId: 'managed-identity-client-id',
      appType: 'UserAssignedMsi',
    });

    expect(factory).toBeInstanceOf(BotFrameworkAudienceAwareManagedIdentityFactory);
    await expect(factory!.isValidAppId(BOT_FRAMEWORK_SERVICE_AUDIENCE)).resolves.toBe(true);
  });
});