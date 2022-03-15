export interface Config {
    httpsPortStateful: number
    httpPortStateless: number
    httpsPortStateless: number
    internalErrorPort: number
    loggingConsoleLevel: string
    serviceEnvironment: string
    serviceName: string
    specRetrievalMethod: string
    specRetrievalGitBranch: string
    specRetrievalGitCommitID: string
    specRetrievalGitUrl: string
    specRetrievalLocalRelativePath: string
    specRetrievalRefreshEnabled: boolean
    specRetrievalGitAuthMode: string
    specRetrievalGitAuthToken: string
    specRetrievalRefreshIntervalMilliseconds: number
    validationPathsPattern: string[]
    excludedValidationPathsPattern: string[]
    enableExampleGeneration: boolean
    exampleGenerationFolder: string
    cascadeEnabled: boolean
}
