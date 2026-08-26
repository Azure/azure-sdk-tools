import { describe, expect, it } from 'vitest';

import {
  BOT_FRAMEWORK_SERVICE_AUDIENCE,
  BotFrameworkAudienceAwareFederatedFactory,
  BotFrameworkAudienceAwareManagedIdentityFactory,
  createBotCredentialsFactory,
} from '../src/auth/botCredentials.js';

describe('bot credential selection', () => {
  it('uses federated credentials when the bot app trusts a managed identity', () => {
    const factory = createBotCredentialsFactory({
      appId: 'bot-app-id',
      appType: 'SingleTenant',
      tenantId: 'tenant-id',
      managedIdentityClientId: 'managed-identity-client-id',
    });

    expect(factory).toBeInstanceOf(BotFrameworkAudienceAwareFederatedFactory);
  });

  it('retains the legacy UAMI path for environments not yet migrated', () => {
    const factory = createBotCredentialsFactory({
      appId: 'managed-identity-client-id',
      appType: 'UserAssignedMsi',
      tenantId: 'tenant-id',
    });

    expect(factory).toBeInstanceOf(BotFrameworkAudienceAwareManagedIdentityFactory);
  });

  it('accepts the bot app ID and Logic App token audiences', async () => {
    const factory = new BotFrameworkAudienceAwareFederatedFactory(
      'bot-app-id',
      'managed-identity-client-id',
      'tenant-id',
    );

    await expect(factory.isValidAppId('bot-app-id')).resolves.toBe(true);
    await expect(factory.isValidAppId(BOT_FRAMEWORK_SERVICE_AUDIENCE)).resolves.toBe(true);
    await expect(factory.isValidAppId('managed-identity-client-id')).resolves.toBe(true);
    await expect(factory.isValidAppId('different-app-id')).resolves.toBe(false);
  });
});