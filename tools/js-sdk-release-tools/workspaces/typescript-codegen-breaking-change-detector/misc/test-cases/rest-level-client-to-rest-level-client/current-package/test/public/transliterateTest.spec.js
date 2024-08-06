"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
const chai_1 = require("chai");
const src_1 = require("../../src");
const recordedClient_1 = require("./utils/recordedClient");
const testHelper_1 = require("./utils/testHelper");
describe("Transliterate tests", () => {
    let recorder;
    let client;
    beforeEach(async function () {
        recorder = await (0, recordedClient_1.startRecorder)(this);
        client = await (0, recordedClient_1.createTranslationClient)({ recorder });
    });
    afterEach(async function () {
        await recorder.stop();
    });
    it("transliterate basic", async () => {
        const inputText = [{ text: "这里怎么一回事?" }];
        const parameters = {
            language: "zh-Hans",
            fromScript: "Hans",
            toScript: "Latn",
        };
        const response = await client.path("/transliterate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].script !== null);
        chai_1.assert.isTrue(translations[0].text !== null);
    });
    it("multiple text array", async () => {
        const inputText = [
            { text: "यहएककसौटीहैयहएककसौटीहै" },
            { text: "यहएककसौटीहै" },
        ];
        const parameters = {
            language: "hi",
            fromScript: "Deva",
            toScript: "Latn",
        };
        const response = await client.path("/transliterate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].script !== null);
        chai_1.assert.isTrue(translations[0].text !== null);
    });
    it("with edit distance", async () => {
        const inputText = [
            { text: "gujarat" },
            { text: "hadman" },
            { text: "hukkabar" },
        ];
        const parameters = {
            language: "gu",
            fromScript: "Latn",
            toScript: "gujr",
        };
        const response = await client.path("/transliterate").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const translations = response.body;
        chai_1.assert.isTrue(translations[0].text !== null);
        chai_1.assert.isTrue(translations[1].text !== null);
        chai_1.assert.isTrue(translations[2].text !== null);
        const expectedText = ["ગુજરાત", "હદમાં", "હુક્કાબાર"];
        let editDistanceValue = 0;
        for (let i = 0; i < expectedText.length; i++) {
            editDistanceValue = editDistanceValue + (0, testHelper_1.editDistance)(expectedText[i], translations[i].text);
        }
        chai_1.assert.isTrue(editDistanceValue < 6);
    });
});
//# sourceMappingURL=transliterateTest.spec.js.map