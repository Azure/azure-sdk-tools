"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
Object.defineProperty(exports, "__esModule", { value: true });
exports.main = main;
const tslib_1 = require("tslib");
/**
 * @summary This sample demonstrates how you can select whether the translated text is plain text or HTML text.
 * Any HTML needs to be a well-formed, complete element. Possible values are: plain (default) or html.
 */
const ai_translation_text_1 = tslib_1.__importStar(require("@azure-rest/ai-translation-text"));
const dotenv = tslib_1.__importStar(require("dotenv"));
dotenv.config();
const endpoint = process.env["ENDPOINT"] || "https://api.cognitive.microsofttranslator.com";
const apiKey = process.env["TEXT_TRANSLATOR_API_KEY"] || "<api key>";
const region = process.env["TEXT_TRANSLATOR_REGION"] || "<region>";
async function main() {
    console.log("== HTML translation sample ==");
    const translateCedential = {
        key: apiKey,
        region,
    };
    const translationClient = (0, ai_translation_text_1.default)(endpoint, translateCedential);
    const inputText = [{ text: "<html><body>This <b>is</b> a test.</body></html>" }];
    const translateResponse = await translationClient.path("/translate").post({
        body: inputText,
        queryParameters: {
            to: "cs",
            from: "en",
            textType: "html",
        },
    });
    if ((0, ai_translation_text_1.isUnexpected)(translateResponse)) {
        throw translateResponse.body.error;
    }
    const translations = translateResponse.body;
    for (const translation of translations) {
        console.log(`Text was translated to: '${translation?.translations[0]?.to}' and the result is: '${translation?.translations[0]?.text}'.`);
    }
}
main().catch((err) => {
    console.error(err);
});
//# sourceMappingURL=translateTextType.js.map