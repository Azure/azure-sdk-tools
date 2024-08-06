"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
Object.defineProperty(exports, "__esModule", { value: true });
exports.main = main;
const tslib_1 = require("tslib");
/**
 * @summary This sample demonstrates how to make a simple call to the Azure Text Translator
 * service to get a list of supported languages for a selected scope
 */
const ai_translation_text_1 = tslib_1.__importStar(require("@azure-rest/ai-translation-text"));
const dotenv = tslib_1.__importStar(require("dotenv"));
dotenv.config();
const endpoint = process.env["ENDPOINT"] || "https://api.cognitive.microsofttranslator.com";
async function main() {
    console.log("== Scoped list supported languages sample ==");
    const parameters = {
        queryParameters: {
            scope: "translation",
        },
    };
    const translationClient = (0, ai_translation_text_1.default)(endpoint, undefined, undefined);
    const langResponse = await translationClient.path("/languages").get(parameters);
    if ((0, ai_translation_text_1.isUnexpected)(langResponse)) {
        throw langResponse.body.error;
    }
    const languages = langResponse.body;
    if (languages.translation) {
        console.log("Translated languages:");
        for (const key in languages.translation) {
            const translationLanguage = languages.translation[key];
            console.log(`${key} -- name: ${translationLanguage.name} (${translationLanguage.nativeName})`);
        }
    }
    if (languages.transliteration) {
        console.log("Transliteration languages:");
        for (const key in languages.transliteration) {
            const transliterationLanguage = languages.transliteration[key];
            console.log(`${key} -- name: ${transliterationLanguage.name} (${transliterationLanguage.nativeName})`);
        }
    }
    if (languages.dictionary) {
        console.log("Dictionary languages:");
        for (const key in languages.dictionary) {
            const dictionaryLanguage = languages.dictionary[key];
            console.log(`${key} -- name: ${dictionaryLanguage.name} (${dictionaryLanguage.nativeName}), supported target languages count: ${dictionaryLanguage.translations.length}`);
        }
    }
}
main().catch((err) => {
    console.error(err);
});
//# sourceMappingURL=getLanguagesScope.js.map