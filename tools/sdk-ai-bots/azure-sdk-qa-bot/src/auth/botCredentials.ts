import {
  FederatedServiceClientCredentialsFactory,
  JwtTokenProviderFactory,
  ManagedIdentityServiceClientCredentialsFactory,
  ServiceClientCredentialsFactory,
} from 'botframework-connector';

export const BOT_FRAMEWORK_SERVICE_AUDIENCE = 'https://api.botframework.com';

export class BotFrameworkAudienceAwareManagedIdentityFactory extends ManagedIdentityServiceClientCredentialsFactory {
  async isValidAppId(appId: string): Promise<boolean> {
    if (appId === BOT_FRAMEWORK_SERVICE_AUDIENCE) {
      return true;
    }
    return super.isValidAppId(appId);
  }
}

export class BotFrameworkAudienceAwareFederatedFactory extends FederatedServiceClientCredentialsFactory {
  constructor(
    appId: string,
    private readonly managedIdentityClientId: string,
    tenantId?: string,
  ) {
    super(appId, managedIdentityClientId, tenantId);
  }

  async isValidAppId(appId: string): Promise<boolean> {
    if (
      appId === BOT_FRAMEWORK_SERVICE_AUDIENCE ||
      appId === this.managedIdentityClientId
    ) {
      return true;
    }
    return super.isValidAppId(appId);
  }
}

export interface BotCredentialOptions {
  appId?: string;
  appType?: string;
  tenantId?: string;
  managedIdentityClientId?: string;
}

export function createBotCredentialsFactory(
  options: BotCredentialOptions,
): ServiceClientCredentialsFactory | undefined {
  const { appId, appType, tenantId, managedIdentityClientId } = options;
  if (appId && managedIdentityClientId) {
    return new BotFrameworkAudienceAwareFederatedFactory(
      appId,
      managedIdentityClientId,
      tenantId,
    );
  }

  if ((appType ?? '').trim().toLowerCase() === 'userassignedmsi' && appId) {
    return new BotFrameworkAudienceAwareManagedIdentityFactory(
      appId,
      new JwtTokenProviderFactory(),
    );
  }

  return undefined;
}