import { AccessToken, TokenCredential } from "@azure/identity";
import { logger } from "../logging/logger.js";

export async function getAccessTokenByManagedIdentity(credential: TokenCredential, scope: string) : Promise<AccessToken | undefined>{
    try {
      logger.info(`get Access Token for ${credential.constructor.name}, scope ${scope}`);
      const token = await credential.getToken(scope);
      logger.info(`Succeed to get Access Token for ${credential.constructor.name}`);
      return token;
    } catch (err) {
      logger.error(`Failed to acquire token for ${credential}`, err.message);
    }

    return undefined;
  }
