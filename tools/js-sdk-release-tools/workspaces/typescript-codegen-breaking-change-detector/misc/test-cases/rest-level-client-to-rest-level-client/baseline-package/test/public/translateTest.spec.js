"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
const chai_1 = require("chai");
const src_1 = require("../../src");
const recordedClient_1 = require("./utils/recordedClient");
describe("Translate tests", () => {
    let recorder;
    let client;
    let customClient;
    beforeEach(async function () {
        recorder = await (0, recordedClient_1.startRecorder)(this);
        client = await (0, recordedClient_1.createTranslationClient)({ recorder });
        customClient = await (0, recordedClient_1.createCustomTranslationClient)({ recorder });
    });
    afterEach(async function () {
        await recorder.stop();
    });
    it("translate basic", async () => {
        const inputText = [{ text: "This is a test." }];
        const parameters = {
            to: "cs",
            from: "en",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].translations.length > 0);
        chai_1.assert.isTrue(translations[0].translations[0].to === "cs");
        chai_1.assert.isTrue(translations[0].translations[0].text !== null);
    });
    it("with auto detect", async () => {
        const inputText = [{ text: "This is a test." }];
        const parameters = {
            to: "cs",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].translations.length > 0);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].to === "cs");
        chai_1.assert.isTrue(translations[0].translations[0].text !== null);
    });
    it("no translate tag", async () => {
        const inputText = [
            { text: "<span class=notranslate>今天是怎么回事是</span>非常可怕的" },
        ];
        const parameters = {
            to: "zh-chs",
            from: "en",
            textType: "html",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations[0].text.includes("今天是怎么回事是"));
    });
    it("dictionary tag", async () => {
        const inputText = [
            {
                text: 'The word < mstrans:dictionary translation ="wordomatic">wordomatic</mstrans:dictionary> is a dictionary entry.',
            },
        ];
        const parameters = {
            to: "es",
            from: "en",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations[0].to === "es");
        chai_1.assert.isTrue(translations[0].translations[0].text.includes("wordomatic"));
    });
    it("transliteration", async () => {
        const inputText = [{ text: "hudha akhtabar." }];
        const parameters = {
            to: "zh-Hans",
            from: "ar",
            fromScript: "Latn",
            toScript: "Latn",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations[0].to === "zh-Hans");
        chai_1.assert.isTrue(translations[0].translations[0].text !== null);
    });
    it("from latin to latin script", async () => {
        const inputText = [{ text: "ap kaise ho" }];
        const parameters = {
            to: "ta",
            from: "hi",
            fromScript: "Latn",
            toScript: "Latn",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations[0].transliteration != null);
        chai_1.assert.isTrue(translations[0].translations[0].transliteration?.text.includes("eppadi irukkiraai?"));
    });
    it("multiple input text", async () => {
        const inputText = [
            { text: "This is a test." },
            { text: "Esto es una prueba." },
            { text: "Dies ist ein Test." },
        ];
        const parameters = {
            to: "cs",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 3);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[1].detectedLanguage?.language === "es");
        chai_1.assert.isTrue(translations[2].detectedLanguage?.language === "de");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[1].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[2].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].text != null);
        chai_1.assert.isTrue(translations[1].translations[0].text != null);
        chai_1.assert.isTrue(translations[2].translations[0].text != null);
    });
    it("multiple target languages", async () => {
        const inputText = [{ text: "This is a test." }];
        const parameters = {
            to: "cs,es,de",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].translations.length === 3);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].text != null);
        chai_1.assert.isTrue(translations[0].translations[1].text != null);
        chai_1.assert.isTrue(translations[0].translations[2].text != null);
    });
    it("different text types", async () => {
        const inputText = [
            { text: "<html><body>This <b>is</b> a test.</body></html>" },
        ];
        const parameters = {
            to: "cs",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
    });
    it("with profanity", async () => {
        const inputText = [{ text: "shit this is fucking crazy" }];
        const parameters = {
            to: "zh-cn",
            profanityAction: "Marked",
            profanityMarker: "Asterisk",
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].text.includes("***"));
    });
    it("with alignment", async () => {
        const inputText = [{ text: "It is a beautiful morning" }];
        const parameters = {
            to: "cs",
            includeAlignment: true,
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].alignment?.proj != null);
    });
    it("with include sentence length", async () => {
        const inputText = [
            {
                text: "La réponse se trouve dans la traduction automatique. La meilleure technologie de traduction automatique ne peut pas toujours fournir des traductions adaptées à un site ou des utilisateurs comme un être humain. Il suffit de copier et coller un extrait de code n'importe où.",
            },
        ];
        const parameters = {
            to: "fr",
            includeSentenceLength: true,
        };
        const response = await client.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "fr");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].sentLen?.srcSentLen.length === 3);
        chai_1.assert.isTrue(translations[0].translations[0].sentLen?.transSentLen.length === 3);
    });
    it("with custom endpoint", async () => {
        const inputText = [{ text: "This is a test." }];
        const parameters = {
            to: "cs",
            includeSentenceLength: true,
        };
        const response = await customClient.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        const translations = response.body;
        chai_1.assert.isTrue(translations.length === 1);
        chai_1.assert.isTrue(translations[0].translations.length === 1);
        chai_1.assert.isTrue(translations[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(translations[0].detectedLanguage?.score === 1);
        chai_1.assert.isTrue(translations[0].translations[0].text != null);
    });
    it("with token", async () => {
        const tokenClient = await (0, recordedClient_1.createTokenTranslationClient)({ recorder });
        const inputText = [{ text: "This is a test." }];
        const parameters = {
            to: "cs",
        };
        const response = await tokenClient.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
    });
    it("with AAD authentication", async () => {
        const tokenClient = await (0, recordedClient_1.createAADAuthenticationTranslationClient)({ recorder });
        const inputText = [{ text: "This is a test." }];
        const parameters = {
            to: "cs",
        };
        const response = await tokenClient.path("/translate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
    });
});
//# sourceMappingURL=translateTest.spec.js.map