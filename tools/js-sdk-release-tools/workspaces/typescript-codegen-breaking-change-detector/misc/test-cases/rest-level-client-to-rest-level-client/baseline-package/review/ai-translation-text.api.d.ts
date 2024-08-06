import { Client } from '@azure-rest/core-client';
import { ClientOptions } from '@azure-rest/core-client';
import { HttpResponse } from '@azure-rest/core-client';
import { KeyCredential } from '@azure/core-auth';
import { RawHttpHeaders } from '@azure/core-rest-pipeline';
import { RawHttpHeadersInput } from '@azure/core-rest-pipeline';
import { RequestParameters } from '@azure-rest/core-client';
import { StreamableMethod } from '@azure-rest/core-client';
import { TokenCredential } from '@azure/core-auth';
export interface BackTranslationOutput {
    displayText: string;
    frequencyCount: number;
    normalizedText: string;
    numExamples: number;
}
export interface BreakSentenceItemOutput {
    detectedLanguage?: DetectedLanguageOutput;
    sentLen: number[];
}
export declare function buildMultiCollection(items: string[], parameterName: string): string;
export interface CommonScriptModelOutput {
    code: string;
    dir: string;
    name: string;
    nativeName: string;
}
declare function createClient(endpoint: undefined | string, credential?: undefined | TranslatorCredential | TranslatorTokenCredential | KeyCredential | TokenCredential, options?: ClientOptions): TextTranslationClient;
export default createClient;
export interface DetectedLanguageOutput {
    language: string;
    score: number;
}
export interface DictionaryExampleItemOutput {
    examples: Array<DictionaryExampleOutput>;
    normalizedSource: string;
    normalizedTarget: string;
}
export interface DictionaryExampleOutput {
    sourcePrefix: string;
    sourceSuffix: string;
    sourceTerm: string;
    targetPrefix: string;
    targetSuffix: string;
    targetTerm: string;
}
export interface DictionaryExampleTextItem extends InputTextItem {
    translation: string;
}
export interface DictionaryLookupItemOutput {
    displaySource: string;
    normalizedSource: string;
    translations: Array<DictionaryTranslationOutput>;
}
export interface DictionaryTranslationOutput {
    backTranslations: Array<BackTranslationOutput>;
    confidence: number;
    displayTarget: string;
    normalizedTarget: string;
    posTag: string;
    prefixWord: string;
}
export interface ErrorDetailsOutput {
    code: number;
    message: string;
}
export interface ErrorResponseOutput {
    error: ErrorDetailsOutput;
}
export interface FindSentenceBoundaries {
    post(options: FindSentenceBoundariesParameters): StreamableMethod<FindSentenceBoundaries200Response | FindSentenceBoundariesDefaultResponse>;
}
export interface FindSentenceBoundaries200Headers {
    'x-requestid': string;
}
export interface FindSentenceBoundaries200Response extends HttpResponse {
    body: Array<BreakSentenceItemOutput>;
    headers: RawHttpHeaders & FindSentenceBoundaries200Headers;
    status: '200';
}
export interface FindSentenceBoundariesBodyParam {
    body: Array<InputTextItem>;
}
export interface FindSentenceBoundariesDefaultHeaders {
    'x-requestid': string;
}
export interface FindSentenceBoundariesDefaultResponse extends HttpResponse {
    body: ErrorResponseOutput;
    headers: RawHttpHeaders & FindSentenceBoundariesDefaultHeaders;
    status: string;
}
export interface FindSentenceBoundariesHeaderParam {
    headers?: RawHttpHeadersInput & FindSentenceBoundariesHeaders;
}
export interface FindSentenceBoundariesHeaders {
    'X-ClientTraceId'?: string;
}
export type FindSentenceBoundariesParameters = FindSentenceBoundariesQueryParam & FindSentenceBoundariesHeaderParam & FindSentenceBoundariesBodyParam & RequestParameters;
export interface FindSentenceBoundariesQueryParam {
    queryParameters?: FindSentenceBoundariesQueryParamProperties;
}
export interface FindSentenceBoundariesQueryParamProperties {
    language?: string;
    script?: string;
}
export interface GetLanguagesLatest {
    getxxx(options?: GetLanguagesParameters): StreamableMethod<GetLanguages200Response | GetLanguagesDefaultResponse>;
}
export interface GetLanguages200Headers {
    'x-requestid': string;
    etag: string;
}
export interface GetLanguages200Response extends HttpResponse {
    body: GetLanguagesResultOutput;
    headers: RawHttpHeaders & GetLanguages200Headers;
    status: '200';
}
export interface GetLanguagesDefaultHeaders {
    'x-requestid': string;
}
export interface GetLanguagesDefaultResponse extends HttpResponse {
    body: ErrorResponseOutput;
    headers: RawHttpHeaders & GetLanguagesDefaultHeaders;
    status: string;
}
export interface GetLanguagesHeaderParam {
    headers?: RawHttpHeadersInput & GetLanguagesHeaders;
}
export interface GetLanguagesHeaders {
    'Accept-Language'?: string;
    'If-None-Match'?: string;
    'X-ClientTraceId'?: string;
}
export type GetLanguagesParameters = GetLanguagesQueryParam & GetLanguagesHeaderParam & RequestParameters;
export interface GetLanguagesQueryParam {
    queryParameters?: GetLanguagesQueryParamProperties;
}
export interface GetLanguagesQueryParamProperties {
    scope?: string;
}
export interface GetLanguagesResultOutput {
    dictionary?: Record<string, SourceDictionaryLanguageOutput>;
    translation?: Record<string, TranslationLanguageOutput>;
    transliteration?: Record<string, TransliterationLanguageOutput>;
}
export interface InputTextItem {
    text: string;
}
export declare function isUnexpected(response: GetLanguages200Response | GetLanguagesDefaultResponse): response is GetLanguagesDefaultResponse;
export declare function isUnexpected(response: Translate200Response | TranslateDefaultResponse): response is TranslateDefaultResponse;
export declare function isUnexpected(response: Transliterate200Response | TransliterateDefaultResponse): response is TransliterateDefaultResponse;
export declare function isUnexpected(response: FindSentenceBoundaries200Response | FindSentenceBoundariesDefaultResponse): response is FindSentenceBoundariesDefaultResponse;
export declare function isUnexpected(response: LookupDictionaryEntries200Response | LookupDictionaryEntriesDefaultResponse): response is LookupDictionaryEntriesDefaultResponse;
export declare function isUnexpected(response: LookupDictionaryExamples200Response | LookupDictionaryExamplesDefaultResponse): response is LookupDictionaryExamplesDefaultResponse;
export interface LookupDictionaryEntries {
    post(options: LookupDictionaryEntriesParameters): StreamableMethod<LookupDictionaryEntries200Response | LookupDictionaryEntriesDefaultResponse>;
}
export interface LookupDictionaryEntries200Headers {
    'x-requestid': string;
}
export interface LookupDictionaryEntries200Response extends HttpResponse {
    body: Array<DictionaryLookupItemOutput>;
    headers: RawHttpHeaders & LookupDictionaryEntries200Headers;
    status: '200';
}
export interface LookupDictionaryEntriesBodyParam {
    body: Array<InputTextItem>;
}
export interface LookupDictionaryEntriesDefaultHeaders {
    'x-requestid': string;
}
export interface LookupDictionaryEntriesDefaultResponse extends HttpResponse {
    body: ErrorResponseOutput;
    headers: RawHttpHeaders & LookupDictionaryEntriesDefaultHeaders;
    status: string;
}
export interface LookupDictionaryEntriesHeaderParam {
    headers?: RawHttpHeadersInput & LookupDictionaryEntriesHeaders;
}
export interface LookupDictionaryEntriesHeaders {
    'X-ClientTraceId'?: string;
}
export type LookupDictionaryEntriesParameters = LookupDictionaryEntriesQueryParam & LookupDictionaryEntriesHeaderParam & LookupDictionaryEntriesBodyParam & RequestParameters;
export interface LookupDictionaryEntriesQueryParam {
    queryParameters: LookupDictionaryEntriesQueryParamProperties;
}
export interface LookupDictionaryEntriesQueryParamProperties {
    from: string;
    to: string;
}
export interface LookupDictionaryExamples {
    post(options: LookupDictionaryExamplesParameters): StreamableMethod<LookupDictionaryExamples200Response | LookupDictionaryExamplesDefaultResponse>;
}
export interface LookupDictionaryExamples200Headers {
    'x-requestid': string;
}
export interface LookupDictionaryExamples200Response extends HttpResponse {
    body: Array<DictionaryExampleItemOutput>;
    headers: RawHttpHeaders & LookupDictionaryExamples200Headers;
    status: '200';
}
export interface LookupDictionaryExamplesBodyParam {
    body: Array<DictionaryExampleTextItem>;
}
export interface LookupDictionaryExamplesDefaultHeaders {
    'x-requestid': string;
}
export interface LookupDictionaryExamplesDefaultResponse extends HttpResponse {
    body: ErrorResponseOutput;
    headers: RawHttpHeaders & LookupDictionaryExamplesDefaultHeaders;
    status: string;
}
export interface LookupDictionaryExamplesHeaderParam {
    headers?: RawHttpHeadersInput & LookupDictionaryExamplesHeaders;
}
export interface LookupDictionaryExamplesHeaders {
    'X-ClientTraceId'?: string;
}
export type LookupDictionaryExamplesParameters = LookupDictionaryExamplesQueryParam & LookupDictionaryExamplesHeaderParam & LookupDictionaryExamplesBodyParam & RequestParameters;
export interface LookupDictionaryExamplesQueryParam {
    queryParameters: LookupDictionaryExamplesQueryParamProperties;
}
export interface LookupDictionaryExamplesQueryParamProperties {
    from: string;
    to: string;
}
export interface Routes {
    (path: '/languages'): GetLanguagesLatest;
    (path: '/translate'): Translate;
    (path: '/transliterate'): Transliterate;
    (path: '/breaksentence'): FindSentenceBoundaries;
    (path: '/dictionary/lookup'): LookupDictionaryEntries;
    (path: '/dictionary/examples'): LookupDictionaryExamples;
}
export interface SentenceLengthOutput {
    srcSentLen: number[];
    transSentLen: number[];
}
export interface SourceDictionaryLanguageOutput {
    dir: string;
    name: string;
    nativeName: string;
    translations: Array<TargetDictionaryLanguageOutput>;
}
export interface SourceTextOutput {
    text: string;
}
export interface TargetDictionaryLanguageOutput {
    code: string;
    dir: string;
    name: string;
    nativeName: string;
}
export type TextTranslationClient = Client & {
    path: Routes;
};
export interface Translate {
    post(options: TranslateParameters): StreamableMethod<Translate200Response | TranslateDefaultResponse>;
}
export interface Translate200Headers {
    'x-metered-usage': number;
    'x-mt-system': string;
    'x-requestid': string;
}
export interface Translate200Response extends HttpResponse {
    body: Array<TranslatedTextItemOutput>;
    headers: RawHttpHeaders & Translate200Headers;
    status: '200';
}
export interface TranslateBodyParam {
    body: Array<InputTextItem>;
}
export interface TranslateDefaultHeaders {
    'x-requestid': string;
}
export interface TranslateDefaultResponse extends HttpResponse {
    body: ErrorResponseOutput;
    headers: RawHttpHeaders & TranslateDefaultHeaders;
    status: string;
}
export interface TranslatedTextAlignmentOutput {
    proj: string;
}
export interface TranslatedTextItemOutput {
    detectedLanguage?: DetectedLanguageOutput;
    sourceText?: SourceTextOutput;
    translations: Array<TranslationOutput>;
}
export interface TranslateHeaderParam {
    headers?: RawHttpHeadersInput & TranslateHeaders;
}
export interface TranslateHeaders {
    'X-ClientTraceId'?: string;
}
export type TranslateParameters = TranslateQueryParam & TranslateHeaderParam & TranslateBodyParam & RequestParameters;
export interface TranslateQueryParam {
    queryParameters: TranslateQueryParamProperties;
}
export interface TranslateQueryParamProperties {
    allowFallback?: boolean;
    category?: string;
    from?: string;
    fromScript?: string;
    includeAlignment?: boolean;
    includeSentenceLength?: boolean;
    profanityAction?: string;
    profanityMarker?: string;
    suggestedFrom?: string;
    textType?: string;
    to: string;
    toScript?: string;
}
export interface TranslationLanguageOutput {
    dir: string;
    name: string;
    nativeName: string;
}
export interface TranslationOutput {
    alignment?: TranslatedTextAlignmentOutput;
    sentLen?: SentenceLengthOutput;
    text: string;
    to: string;
    transliteration?: TransliteratedTextOutput;
}
export interface TranslatorCredential {
    key: string;
    region: string;
}
export interface TranslatorTokenCredential {
    azureResourceId: string;
    region: string;
    tokenCredential: TokenCredential;
}
export interface TransliterableScriptOutput extends CommonScriptModelOutput {
    toScripts: Array<CommonScriptModelOutput>;
}
export interface Transliterate {
    post(options: TransliterateParameters): StreamableMethod<Transliterate200Response | TransliterateDefaultResponse>;
}
export interface Transliterate200Headers {
    'x-requestid': string;
}
export interface Transliterate200Response extends HttpResponse {
    body: Array<TransliteratedTextOutput>;
    headers: RawHttpHeaders & Transliterate200Headers;
    status: '200';
}
export interface TransliterateBodyParam {
    body: Array<InputTextItem>;
}
export interface TransliterateDefaultHeaders {
    'x-requestid': string;
}
export interface TransliterateDefaultResponse extends HttpResponse {
    body: ErrorResponseOutput;
    headers: RawHttpHeaders & TransliterateDefaultHeaders;
    status: string;
}
export interface TransliteratedTextOutput {
    script: string;
    text: string;
}
export interface TransliterateHeaderParam {
    headers?: RawHttpHeadersInput & TransliterateHeaders;
}
export interface TransliterateHeaders {
    'X-ClientTraceId'?: string;
}
export type TransliterateParameters = TransliterateQueryParam & TransliterateHeaderParam & TransliterateBodyParam & RequestParameters;
export interface TransliterateQueryParam {
    queryParameters: TransliterateQueryParamProperties;
}
export interface TransliterateQueryParamProperties {
    fromScript: string;
    language: string;
    toScript: string;
}
export interface TransliterationLanguageOutput {
    name: string;
    nativeName: string;
    scripts: Array<TransliterableScriptOutput>;
}
//# sourceMappingURL=ai-translation-text.api.d.ts.map