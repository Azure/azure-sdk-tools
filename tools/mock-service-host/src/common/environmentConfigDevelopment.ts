import { Config } from './config'

export const environmentConfigDevelopment: Partial<Config> = {
    loggingConsoleLevel: 'silly',
    specRetrievalGitUrl: 'https://github.com/Azure/azure-rest-api-specs',
    specRetrievalGitBranch: 'main',
    specRetrievalGitCommitID: '',
    specRetrievalLocalRelativePath: './cache',
    specRetrievalRefreshEnabled: false,
    specRetrievalRefreshIntervalMilliseconds: 300000,
    validationPathsPattern: [
        'specification/apimanagement/resource-manager/Microsoft.ApiManagement/**/*.json',
        'specification/mediaservices/resource-manager/Microsoft.Media/**/*.json'
    ]
}
