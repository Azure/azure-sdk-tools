import { Context } from "mocha";
import { Recorder } from "@azure-tools/test-recorder";
import { TextTranslationClient } from "../../../src";
import { ClientOptions } from "@azure-rest/core-client";
export declare function startRecorder(context: Context): Promise<Recorder>;
export declare function createTranslationClient(options: {
    recorder?: Recorder;
    clientOptions?: ClientOptions;
}): Promise<TextTranslationClient>;
export declare function createCustomTranslationClient(options: {
    recorder?: Recorder;
    clientOptions?: ClientOptions;
}): Promise<TextTranslationClient>;
export declare function createLanguageClient(options: {
    recorder?: Recorder;
    clientOptions?: ClientOptions;
}): Promise<TextTranslationClient>;
export declare function createTokenTranslationClient(options: {
    recorder?: Recorder;
    clientOptions?: ClientOptions;
}): Promise<TextTranslationClient>;
export declare function createAADAuthenticationTranslationClient(options: {
    recorder?: Recorder;
    clientOptions?: ClientOptions;
}): Promise<TextTranslationClient>;
export declare function createMockToken(): {
    getToken: (_scopes: string) => Promise<{
        token: string;
        expiresOnTimestamp: number;
    }>;
};
//# sourceMappingURL=recordedClient.d.ts.map