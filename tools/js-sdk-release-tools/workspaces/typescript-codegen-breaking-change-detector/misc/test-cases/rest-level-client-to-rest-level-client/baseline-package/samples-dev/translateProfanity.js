"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
Object.defineProperty(exports, "__esModule", { value: true });
exports.main = main;
const tslib_1 = require("tslib");
/**
 * @summary This sample demonstrates how to change the profanity handling during translate call.
 * Normally the Translator service will retain profanity that is present in the source
 * in the translation. The degree of profanity and the context that makes words profane
 * differ between cultures, and as a result the degree of profanity in the target language
 * may be amplified or reduced.
 *
 * If you want to avoid getting profanity in the translation, regardless of the presence
 * of profanity in the source text, you can use the profanity filtering option. The option
 * allows you to choose whether you want to see profanity deleted, whether you want to mark
 * profanities with appropriate tags (giving you the option to add your own post-processing),
 * or you want no action taken. The accepted values of `ProfanityAction` are `Deleted`, `Marked`
 * and `NoAction` (default).
 */
const ai_translation_text_1 = tslib_1.__importStar(require("@azure-rest/ai-translation-text"));
const dotenv = tslib_1.__importStar(require("dotenv"));
dotenv.config();
const endpoint = process.env["ENDPOINT"] || "https://api.cognitive.microsofttranslator.com";
const apiKey = process.env["TEXT_TRANSLATOR_API_KEY"] || "<api key>";
const region = process.env["TEXT_TRANSLATOR_REGION"] || "<region>";
async function main() {
    console.log("== Profanity handling sample ==");
    const translateCedential = {
        key: apiKey,
        region,
    };
    const translationClient = (0, ai_translation_text_1.default)(endpoint, translateCedential);
    const inputText = [{ text: "This is ***." }];
    const translateResponse = await translationClient.path("/translate").post({
        body: inputText,
        queryParameters: {
            to: "cs",
            from: "en",
            profanityAction: "Marked",
            profanityMarker: "Asterisk",
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
//# sourceMappingURL=translateProfanity.js.map