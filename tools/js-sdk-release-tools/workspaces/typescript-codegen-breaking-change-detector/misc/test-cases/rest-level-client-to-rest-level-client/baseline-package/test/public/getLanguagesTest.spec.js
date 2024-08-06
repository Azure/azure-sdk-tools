"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
const chai_1 = require("chai");
const recordedClient_1 = require("./utils/recordedClient");
describe("GetLanguages tests", () => {
    let recorder;
    let client;
    beforeEach(async function () {
        recorder = await (0, recordedClient_1.startRecorder)(this);
        client = await (0, recordedClient_1.createLanguageClient)({ recorder });
    });
    afterEach(async function () {
        await recorder.stop();
    });
    it("all scopes", async () => {
        const response = await client.path("/languages").get();
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.translation !== null);
        chai_1.assert.isTrue(languages.transliteration !== null);
        chai_1.assert.isTrue(languages.dictionary !== null);
    });
    it("translation scope", async () => {
        const parameters = {
            queryParameters: {
                scope: "translation",
            },
        };
        const response = await client.path("/languages").get(parameters);
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.translation !== null);
        chai_1.assert.isTrue(languages?.translation?.["af"]?.dir !== null);
        chai_1.assert.isTrue(languages?.translation?.["af"]?.name !== null);
        chai_1.assert.isTrue(languages?.translation?.["af"]?.nativeName !== null);
    });
    it("transliteration scope", async () => {
        const parameters = {
            queryParameters: {
                scope: "transliteration",
            },
        };
        const response = await client.path("/languages").get(parameters);
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.transliteration !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.name !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.nativeName !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].code !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].dir !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].name !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].nativeName !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].toScripts !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].toScripts[0].code !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].toScripts[0].dir !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].toScripts[0].name !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["be"]?.scripts[0].toScripts[0].nativeName !== null);
    });
    it("transliteration scope multiple scripts", async () => {
        const parameters = {
            queryParameters: {
                scope: "transliteration",
            },
        };
        const response = await client.path("/languages").get(parameters);
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.transliteration !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["zh-Hant"]?.name !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["zh-Hant"]?.nativeName !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["zh-Hant"]?.scripts !== null);
        chai_1.assert.isTrue(languages?.transliteration?.["zh-Hant"]?.scripts?.length === 2);
        chai_1.assert.isTrue(languages?.transliteration?.["zh-Hant"]?.scripts[0].toScripts.length === 2);
        chai_1.assert.isTrue(languages?.transliteration?.["zh-Hant"]?.scripts[1].toScripts.length === 2);
    });
    it("dictionary scope", async () => {
        const parameters = {
            queryParameters: {
                scope: "dictionary",
            },
        };
        const response = await client.path("/languages").get(parameters);
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.dictionary !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.name !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.nativeName !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.translations !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.translations[0].code !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.translations[0].dir !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.translations[0].name !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["de"]?.translations[0].nativeName !== null);
    });
    it("dictionary scope with multiple translations", async () => {
        const parameters = {
            queryParameters: {
                scope: "dictionary",
            },
        };
        const response = await client.path("/languages").get(parameters);
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.dictionary !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["en"]?.name !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["en"]?.nativeName !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["en"]?.translations !== null);
        chai_1.assert.isTrue(languages?.dictionary?.["en"]?.translations?.length !== 1);
    });
    it("with culture", async () => {
        const parameters = {
            headers: {
                "Accept-Language": "es",
            },
        };
        const response = await client.path("/languages").get(parameters);
        chai_1.assert.equal("200", response.status);
        const languages = response.body;
        chai_1.assert.isTrue(languages.translation !== null);
        chai_1.assert.isTrue(languages.transliteration !== null);
        chai_1.assert.isTrue(languages.dictionary !== null);
        chai_1.assert.isTrue(languages?.translation?.["en"]?.name !== null);
        chai_1.assert.isTrue(languages?.translation?.["en"]?.nativeName !== null);
        chai_1.assert.isTrue(languages?.translation?.["en"]?.dir !== null);
    });
});
//# sourceMappingURL=getLanguagesTest.spec.js.map