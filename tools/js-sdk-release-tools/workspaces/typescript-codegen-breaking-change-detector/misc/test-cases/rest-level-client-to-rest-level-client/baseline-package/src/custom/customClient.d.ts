import { ClientOptions } from "@azure-rest/core-client";
import { TextTranslationClient } from "../clientDefinitions";
import { TranslatorCredential, TranslatorTokenCredential } from "./authentication";
import { KeyCredential, TokenCredential } from "@azure/core-auth";
/**
 * Initialize a new instance of `TextTranslationClient`
 * @param endpoint type: string, Supported Text Translation endpoints (protocol and hostname, for example:
 *     https://api.cognitive.microsofttranslator.com).
 * @param options type: ClientOptions, the parameter for all optional parameters
 */
export default function createClient(endpoint: undefined | string, credential?: undefined | TranslatorCredential | TranslatorTokenCredential | KeyCredential | TokenCredential, options?: ClientOptions): TextTranslationClient;
//# sourceMappingURL=customClient.d.ts.map