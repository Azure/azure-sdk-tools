"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
const chai_1 = require("chai");
const src_1 = require("../../src");
const recordedClient_1 = require("./utils/recordedClient");
describe("BreakSentence tests", () => {
    let recorder;
    let client;
    beforeEach(async function () {
        recorder = await (0, recordedClient_1.startRecorder)(this);
        client = await (0, recordedClient_1.createTranslationClient)({ recorder });
    });
    afterEach(async function () {
        await recorder.stop();
    });
    it("auto detect", async () => {
        const inputText = [{ text: "hello world" }];
        const response = await client.path("/breaksentence").post({
            body: inputText,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const breakSentences = response.body;
        chai_1.assert.isTrue(breakSentences[0].detectedLanguage?.language === "en");
        chai_1.assert.isTrue(breakSentences[0].detectedLanguage?.score === 0.98);
        chai_1.assert.isTrue(breakSentences[0].sentLen[0] === 11);
    });
    it("with language", async () => {
        const inputText = [
            {
                text: "รวบรวมแผ่นคำตอบ ระยะเวลาของโครงการ วิธีเลือกชายในฝัน หมายเลขซีเรียลของระเบียน วันที่สิ้นสุดของโครงการเมื่อเสร็จสมบูรณ์ ปีที่มีการรวบรวม ทุกคนมีวัฒนธรรมและวิธีคิดเหมือนกัน ได้รับโทษจำคุกตลอดชีวิตใน ฉันลดได้ถึง 55 ปอนด์ได้อย่างไร  ฉันคิดว่าใครๆ ก็ต้องการกำหนดเมนูอาหารส่วนบุคคล",
            },
        ];
        const parameters = {
            language: "th",
        };
        const response = await client.path("/breaksentence").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const breakSentences = response.body;
        const expectedLengths = [78, 41, 110, 46];
        for (let i = 0; i < expectedLengths.length; i++) {
            chai_1.assert.equal(expectedLengths[i], breakSentences[0].sentLen[i]);
        }
    });
    it("with language and script", async () => {
        const inputText = [{ text: "zhè shì gè cè shì。" }];
        const parameters = {
            language: "zh-Hans",
            script: "Latn",
        };
        const response = await client.path("/breaksentence").post({
            body: inputText,
            queryParameters: parameters,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const breakSentences = response.body;
        chai_1.assert.equal(breakSentences[0].sentLen[0], 18);
    });
    it("with multiple languages", async () => {
        const inputText = [
            { text: "hello world" },
            { text: "العالم هو مكان مثير جدا للاهتمام" },
        ];
        const response = await client.path("/breaksentence").post({
            body: inputText,
        });
        chai_1.assert.equal(response.status, "200");
        if ((0, src_1.isUnexpected)(response)) {
            throw response.body;
        }
        const breakSentences = response.body;
        chai_1.assert.equal(breakSentences[0].detectedLanguage?.language, "en");
        chai_1.assert.equal(breakSentences[1].detectedLanguage?.language, "ar");
        chai_1.assert.equal(breakSentences[0].sentLen[0], 11);
        chai_1.assert.equal(breakSentences[1].sentLen[0], 32);
    });
});
//# sourceMappingURL=breakSentenceTest.spec.js.map