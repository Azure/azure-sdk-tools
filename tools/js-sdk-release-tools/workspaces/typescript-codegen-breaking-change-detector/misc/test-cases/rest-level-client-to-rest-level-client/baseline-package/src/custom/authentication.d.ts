import { AzureKeyCredential, TokenCredential } from "@azure/core-auth";
import { PipelinePolicy, PipelineRequest, PipelineResponse, SendRequest } from "@azure/core-rest-pipeline";
export declare const DEFAULT_SCOPE = "https://cognitiveservices.azure.com/.default";
export interface TranslatorCredential {
    key: string;
    region: string;
}
export interface TranslatorTokenCredential {
    tokenCredential: TokenCredential;
    region: string;
    azureResourceId: string;
}
export declare class TranslatorAuthenticationPolicy implements PipelinePolicy {
    name: string;
    credential: TranslatorCredential;
    constructor(credential: TranslatorCredential);
    sendRequest(request: PipelineRequest, next: SendRequest): Promise<PipelineResponse>;
}
export declare class TranslatorAzureKeyAuthenticationPolicy implements PipelinePolicy {
    name: string;
    credential: AzureKeyCredential;
    constructor(credential: AzureKeyCredential);
    sendRequest(request: PipelineRequest, next: SendRequest): Promise<PipelineResponse>;
}
export declare class TranslatorTokenCredentialAuthenticationPolicy implements PipelinePolicy {
    name: string;
    credential: TranslatorTokenCredential;
    constructor(credential: TranslatorTokenCredential);
    sendRequest(request: PipelineRequest, next: SendRequest): Promise<PipelineResponse>;
}
//# sourceMappingURL=authentication.d.ts.map