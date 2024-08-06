"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
exports.startRecorder = startRecorder;
exports.createTranslationClient = createTranslationClient;
exports.createCustomTranslationClient = createCustomTranslationClient;
exports.createLanguageClient = createLanguageClient;
exports.createTokenTranslationClient = createTokenTranslationClient;
exports.createAADAuthenticationTranslationClient = createAADAuthenticationTranslationClient;
exports.createMockToken = createMockToken;
const tslib_1 = require("tslib");
const test_recorder_1 = require("@azure-tools/test-recorder");
const StaticAccessTokenCredential_1 = require("./StaticAccessTokenCredential");
const src_1 = tslib_1.__importDefault(require("../../../src"));
const core_rest_pipeline_1 = require("@azure/core-rest-pipeline");
const identity_1 = require("@azure/identity");
const envSetupForPlayback = {
    TEXT_TRANSLATION_API_KEY: "fakeapikey",
    TEXT_TRANSLATION_ENDPOINT: "https://fakeEndpoint.cognitive.microsofttranslator.com",
    TEXT_TRANSLATION_CUSTOM_ENDPOINT: "https://fakeCustomEndpoint.cognitiveservices.azure.com",
    TEXT_TRANSLATION_REGION: "fakeregion",
    TEXT_TRANSLATION_AAD_REGION: "fakeregion",
    TEXT_TRANSLATION_RESOURCE_ID: "fakeresourceid",
};
const recorderEnvSetup = {
    envSetupForPlayback,
};
async function startRecorder(context) {
    const recorder = new test_recorder_1.Recorder(context.currentTest);
    await recorder.start(recorderEnvSetup);
    return recorder;
}
async function createTranslationClient(options) {
    const { recorder, clientOptions = {} } = options;
    const updatedOptions = recorder ? recorder.configureClientOptions(clientOptions) : clientOptions;
    const endpoint = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_ENDPOINT");
    const apikey = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_API_KEY");
    const region = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_REGION");
    const translatorCredential = {
        key: apikey,
        region,
    };
    const client = (0, src_1.default)(endpoint, translatorCredential, updatedOptions);
    return client;
}
async function createCustomTranslationClient(options) {
    const { recorder, clientOptions = {} } = options;
    const updatedOptions = recorder ? recorder.configureClientOptions(clientOptions) : clientOptions;
    const customEndpoint = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_CUSTOM_ENDPOINT");
    const apikey = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_API_KEY");
    const region = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_REGION");
    const translatorCredential = {
        key: apikey,
        region,
    };
    const client = (0, src_1.default)(customEndpoint, translatorCredential, updatedOptions);
    return client;
}
async function createLanguageClient(options) {
    const { recorder, clientOptions = {} } = options;
    const updatedOptions = recorder ? recorder.configureClientOptions(clientOptions) : clientOptions;
    const endpoint = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_ENDPOINT");
    return (0, src_1.default)(endpoint, undefined, updatedOptions);
}
async function createTokenTranslationClient(options) {
    const { recorder, clientOptions = {} } = options;
    const updatedOptions = recorder ? recorder.configureClientOptions(clientOptions) : clientOptions;
    const endpoint = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_ENDPOINT");
    const apikey = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_API_KEY");
    const region = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_REGION");
    const issueTokenURL = "https://" +
        region +
        ".api.cognitive.microsoft.com/sts/v1.0/issueToken?Subscription-Key=" +
        apikey;
    let credential;
    if ((0, test_recorder_1.isPlaybackMode)()) {
        credential = createMockToken();
    }
    else {
        const tokenClient = (0, core_rest_pipeline_1.createDefaultHttpClient)();
        const request = (0, core_rest_pipeline_1.createPipelineRequest)({
            url: issueTokenURL,
            method: "POST",
        });
        request.allowInsecureConnection = true;
        const response = await tokenClient.sendRequest(request);
        const token = response.bodyAsText;
        credential = new StaticAccessTokenCredential_1.StaticAccessTokenCredential(token);
    }
    const client = (0, src_1.default)(endpoint, credential, updatedOptions);
    return client;
}
async function createAADAuthenticationTranslationClient(options) {
    const { recorder, clientOptions = {} } = options;
    const updatedOptions = recorder ? recorder.configureClientOptions(clientOptions) : clientOptions;
    const endpoint = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_ENDPOINT");
    const region = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_AAD_REGION");
    const azureResourceId = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_RESOURCE_ID");
    let tokenCredential;
    if ((0, test_recorder_1.isPlaybackMode)()) {
        tokenCredential = createMockToken();
    }
    else {
        const clientId = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_CLIENT_ID");
        const tenantId = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_TENANT_ID");
        const secret = (0, test_recorder_1.assertEnvironmentVariable)("TEXT_TRANSLATION_CLIENT_SECRET");
        tokenCredential = new identity_1.ClientSecretCredential(tenantId, clientId, secret);
    }
    const translatorTokenCredentials = {
        tokenCredential,
        azureResourceId,
        region,
    };
    const client = (0, src_1.default)(endpoint, translatorTokenCredentials, updatedOptions);
    return client;
}
function createMockToken() {
    return {
        getToken: async (_scopes) => {
            return { token: "testToken", expiresOnTimestamp: 11111 };
        },
    };
}
//# sourceMappingURL=recordedClient.js.map