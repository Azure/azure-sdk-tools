import { Config } from './config'

export const environmentConfigTest: Partial<Config> = {
    loggingConsoleLevel: 'silly',
    specRetrievalGitUrl: 'https://github.com/Azure/azure-rest-api-specs',
    specRetrievalGitBranch: 'main',
    specRetrievalGitCommitID: '',
    specRetrievalLocalRelativePath: 'test/testData/swaggers',
    specRetrievalMethod: 'filesystem',
    specRetrievalRefreshEnabled: false,
    specRetrievalRefreshIntervalMilliseconds: 300000,
    validationPathsPattern: [
        'specification/apimanagement/resource-manager/Microsoft.ApiManagement/preview/2018-01-01/*.json',
        'specification/mediaservices/resource-manager/Microsoft.Media/**/*.json'
    ],
    enableExampleGeneration: false
}
