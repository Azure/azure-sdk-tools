export const SWAGGER_ENCODING = 'utf8'
export const useREF = '$ref'
export const LRO_CALLBACK = 'lro-callback'

export const mockedResourceType = 'Microsoft.Resources/mockResource'

export enum Headers {
    ExampleId = 'example-id',
    ContentType = 'Content-Type'
}

export enum ParameterType {
    Body = 'body',
    Path = 'path',
    Query = 'query',
    Header = 'header'
}

export enum AzureExtensions {
    XMsLongRunningOperation = 'x-ms-long-running-operation',
    XMsExamples = 'x-ms-examples',
    XMsCorrelationRequestId = 'x-ms-correlation-request-id',
    XMsPaths = 'x-ms-paths'
}
