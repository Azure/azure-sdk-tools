import {
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

export interface BotCredentialOptions {
  appId?: string;
  appType?: string;
}

export function createBotCredentialsFactory(
  options: BotCredentialOptions,
): ServiceClientCredentialsFactory | undefined {
  const { appId, appType } = options;
  if ((appType ?? '').trim().toLowerCase() === 'userassignedmsi' && appId) {
    return new BotFrameworkAudienceAwareManagedIdentityFactory(
      appId,
      new JwtTokenProviderFactory(),
    );
  }

  return undefined;
}