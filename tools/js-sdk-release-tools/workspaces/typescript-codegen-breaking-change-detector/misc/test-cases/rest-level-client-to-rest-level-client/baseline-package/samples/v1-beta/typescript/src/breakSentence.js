"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
Object.defineProperty(exports, "__esModule", { value: true });
exports.main = main;
const tslib_1 = require("tslib");
/**
 * @summary This sample demonstrates how to make a simple call to the Azure Text Translator service to get sentences' boundaries.
 */
const ai_translation_text_1 = tslib_1.__importStar(require("@azure-rest/ai-translation-text"));
const dotenv = tslib_1.__importStar(require("dotenv"));
dotenv.config();
const endpoint = process.env["ENDPOINT"] || "https://api.cognitive.microsofttranslator.com";
const apiKey = process.env["TEXT_TRANSLATOR_API_KEY"] || "<api key>";
const region = process.env["TEXT_TRANSLATOR_REGION"] || "<region>";
async function main() {
    console.log("== Get Sentence Boundaries sample ==");
    const translateCedential = {
        key: apiKey,
        region
    };
    const translationClient = (0, ai_translation_text_1.default)(endpoint, translateCedential);
    const inputText = [{ text: "zhè shì gè cè shì。" }];
    const breakSentenceResponse = await translationClient.path("/breaksentence").post({
        body: inputText,
        queryParameters: {
            language: "zh-Hans",
            script: "Latn",
        }
    });
    if ((0, ai_translation_text_1.isUnexpected)(breakSentenceResponse)) {
        throw breakSentenceResponse.body.error;
    }
    const breakSentences = breakSentenceResponse.body;
    for (const breakSentence of breakSentences) {
        console.log(`The detected sentece boundaries: '${breakSentence?.sentLen.join(", ")}'.`);
    }
}
main().catch((err) => {
    console.error(err);
});
//# sourceMappingURL=breakSentence.js.map