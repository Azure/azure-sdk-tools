"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
exports.TranslatorTokenCredentialAuthenticationPolicy = exports.TranslatorAzureKeyAuthenticationPolicy = exports.TranslatorAuthenticationPolicy = exports.DEFAULT_SCOPE = void 0;
const APIM_KEY_HEADER_NAME = "Ocp-Apim-Subscription-Key";
const APIM_REGION_HEADER_NAME = "Ocp-Apim-Subscription-Region";
const APIM_RESOURCE_ID = "Ocp-Apim-ResourceId";
exports.DEFAULT_SCOPE = "https://cognitiveservices.azure.com/.default";
class TranslatorAuthenticationPolicy {
    name = "TranslatorAuthenticationPolicy";
    credential;
    constructor(credential) {
        this.credential = credential;
    }
    sendRequest(request, next) {
        request.headers.set(APIM_KEY_HEADER_NAME, this.credential.key);
        request.headers.set(APIM_REGION_HEADER_NAME, this.credential.region);
        return next(request);
    }
}
exports.TranslatorAuthenticationPolicy = TranslatorAuthenticationPolicy;
class TranslatorAzureKeyAuthenticationPolicy {
    name = "TranslatorAzureKeyAuthenticationPolicy";
    credential;
    constructor(credential) {
        this.credential = credential;
    }
    sendRequest(request, next) {
        request.headers.set(APIM_KEY_HEADER_NAME, this.credential.key);
        return next(request);
    }
}
exports.TranslatorAzureKeyAuthenticationPolicy = TranslatorAzureKeyAuthenticationPolicy;
class TranslatorTokenCredentialAuthenticationPolicy {
    name = "TranslatorTokenCredentialAuthenticationPolicy";
    credential;
    constructor(credential) {
        this.credential = credential;
    }
    sendRequest(request, next) {
        request.headers.set(APIM_REGION_HEADER_NAME, this.credential.region);
        request.headers.set(APIM_RESOURCE_ID, this.credential.azureResourceId);
        return next(request);
    }
}
exports.TranslatorTokenCredentialAuthenticationPolicy = TranslatorTokenCredentialAuthenticationPolicy;
//# sourceMappingURL=authentication.js.map