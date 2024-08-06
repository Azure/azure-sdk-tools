"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
const chai_1 = require("chai");
const src_1 = require("../../src");
const recordedClient_1 = require("./utils/recordedClient");
describe("DictionaryLookup tests", () => {
    let recorder;
    let client;
    beforeEach(async function () {
        recorder = await (0, recordedClient_1.startRecorder)(this);
        client = await (0, recordedClient_1.createTranslationClient)({ recorder });
    });
    afterEach(async function () {
        await recorder.stop();
    });
    it("single input element", async () => {
        const inputText = [{ text: "fly" }];
        const parameters = {
            to: "es",
            from: "en",
        };
        const response = await client.path("/dictionary/lookup").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const dictionaryEntries = response.body;
        chai_1.assert.isTrue(dictionaryEntries[0].normalizedSource === "fly");
        chai_1.assert.isTrue(dictionaryEntries[0].displaySource === "fly");
    });
    it("multiple input elements", async () => {
        const inputText = [{ text: "fly" }, { text: "fox" }];
        const parameters = {
            to: "es",
            from: "en",
        };
        const response = await client.path("/dictionary/lookup").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const dictionaryEntries = response.body;
        chai_1.assert.isTrue(dictionaryEntries.length === 2);
    });
});
//# sourceMappingURL=dictionaryLookupTest.spec.js.map