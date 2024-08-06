"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = createClient;
const tslib_1 = require("tslib");
const core_client_1 = require("@azure-rest/core-client");
const logger_1 = require("../logger");
const coreRestPipeline = tslib_1.__importStar(require("@azure/core-rest-pipeline"));
const authentication_1 = require("./authentication");
const DEFAULT_ENPOINT = "https://api.cognitive.microsofttranslator.com";
const PLATFORM_HOST = "cognitiveservices";
const PLATFORM_PATH = "/translator/text/v3.0";
function isKeyCredential(credential) {
    return credential?.key !== undefined;
}
function isTranslatorKeyCredential(credential) {
    return credential?.key !== undefined;
}
function isTokenCredential(credential) {
    return credential?.getToken !== undefined;
}
function isTranslatorTokenCredential(credential) {
    return (credential?.tokenCredential !== undefined &&
        credential?.azureResourceId !== undefined);
}
/**
 * Initialize a new instance of `TextTranslationClient`
 * @param endpoint type: string, Supported Text Translation endpoints (protocol and hostname, for example:
 *     https://api.cognitive.microsofttranslator.com).
 * @param options type: ClientOptions, the parameter for all optional parameters
 */
function createClient(endpoint, credential = undefined, options = {}) {
    let serviceEndpoint;
    options.apiVersion = options.apiVersion ?? "3.0";
    if (!endpoint) {
        serviceEndpoint = DEFAULT_ENPOINT;
    }
    else if (endpoint.toLowerCase().indexOf(PLATFORM_HOST) !== -1) {
        serviceEndpoint = `${endpoint}${PLATFORM_PATH}`;
    }
    else {
        serviceEndpoint = endpoint;
    }
    const baseUrl = options.baseUrl ?? `${serviceEndpoint}`;
    const userAgentInfo = `azsdk-js-ai-translation-text-rest/1.0.0-beta.2`;
    const userAgentPrefix = options.userAgentOptions && options.userAgentOptions.userAgentPrefix
        ? `${options.userAgentOptions.userAgentPrefix} ${userAgentInfo}`
        : `${userAgentInfo}`;
    options = {
        ...options,
        userAgentOptions: {
            userAgentPrefix,
        },
        loggingOptions: {
            logger: options.loggingOptions?.logger ?? logger_1.logger.info,
        },
    };
    const client = (0, core_client_1.getClient)(baseUrl, options);
    if (isTranslatorKeyCredential(credential)) {
        const mtAuthneticationPolicy = new authentication_1.TranslatorAuthenticationPolicy(credential);
        client.pipeline.addPolicy(mtAuthneticationPolicy);
    }
    else if (isKeyCredential(credential)) {
        const mtKeyAuthenticationPolicy = new authentication_1.TranslatorAzureKeyAuthenticationPolicy(credential);
        client.pipeline.addPolicy(mtKeyAuthenticationPolicy);
    }
    else if (isTokenCredential(credential)) {
        client.pipeline.addPolicy(coreRestPipeline.bearerTokenAuthenticationPolicy({
            credential: credential,
            scopes: authentication_1.DEFAULT_SCOPE,
        }));
    }
    else if (isTranslatorTokenCredential(credential)) {
        client.pipeline.addPolicy(coreRestPipeline.bearerTokenAuthenticationPolicy({
            credential: credential.tokenCredential,
            scopes: authentication_1.DEFAULT_SCOPE,
        }));
        client.pipeline.addPolicy(new authentication_1.TranslatorTokenCredentialAuthenticationPolicy(credential));
    }
    return client;
}
//# sourceMappingURL=customClient.js.map