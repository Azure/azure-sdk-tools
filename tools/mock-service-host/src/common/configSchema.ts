import { Config } from './config'
import { Environment, SpecRetrievalAuthMode, SpecRetrievalMethod } from './environment'

import convict from 'convict'
/**
 * Service configuration schema.
 */
export const configSchema = convict<Config>({
    httpsPortStateful: {
        doc: 'The http port to use for stateful status.',
        format: 'port',
        default: 8441,
        env: 'httpsPortStateful'
    },
    httpPortStateless: {
        doc: 'The http port to use for stateless status.',
        format: 'port',
        default: 8442,
        env: 'httpPortStateless'
    },
    httpsPortStateless: {
        doc: 'The https port to use for stateful status.',
        format: 'port',
        default: 8443,
        env: 'httpsPortStateless'
    },
    internalErrorPort: {
        doc: 'The https port to use for internal errors.',
        format: 'port',
        default: 8445,
        env: 'internalErrorPort'
    },
    loggingConsoleLevel: {
        doc: 'The logging level to use for console output.',
        format: ['error', 'warn', 'info', 'verbose', 'debug', 'silly'],
        default: 'info',
        env: 'loggingConsoleLevel'
    },
    serviceEnvironment: {
        doc: 'The application environment.',
        format: [Environment.Production, Environment.Test, Environment.Development],
        default: Environment.Development,
        env: 'NODE_ENV'
    },
    serviceName: {
        doc: 'The name of the service.',
        format: String,
        default: 'validate-v2',
        env: 'serviceName'
    },
    specRetrievalMethod: {
        doc: 'The method to use for retrieving specifications',
        format: [SpecRetrievalMethod.Git, SpecRetrievalMethod.Filesystem],
        default: SpecRetrievalMethod.Git,
        env: 'specRetrievalMethod'
    },
    specRetrievalGitBranch: {
        doc: 'The branch to use when pulling specs from a git repo.',
        format: String,
        default: 'main',
        env: 'specRetrievalGitBranch'
    },
    specRetrievalGitCommitID: {
        doc: 'The commitid to use when pulling specs from a git repo.',
        format: String,
        default: '',
        env: 'specRetrievalGitCommitID'
    },
    specRetrievalGitUrl: {
        doc:
            'The repo URL to use when pulling specs from git. To authenticate with GitHub, use https and provide values for authorization header (user name, personal access token) in the URL, e.g. https://<user>:<token>@<repo>',
        format: String,
        default: 'https://github.com/Azure/azure-rest-api-specs',
        env: 'specRetrievalGitUrl'
    },
    specRetrievalLocalRelativePath: {
        doc:
            "The relative path for caching specifications underneath the service's current working directory. NOTE: This directory will be deleted if it exists.",
        format: String,
        default: 'specificationCache',
        env: 'specRetrievalLocalRelativePath'
    },
    specRetrievalRefreshEnabled: {
        doc:
            'If true the validator will refresh specifications on an interval specified by specRetrievalRefreshIntervalMilliseconds.',
        format: Boolean,
        default: true,
        env: 'specRetrievalRefreshEnabled'
    },
    specRetrievalRefreshIntervalMilliseconds: {
        doc:
            'How often to refresh the validator from the source sepcifications, specified in milliseconds.',
        format: Number,
        default: 3600000,
        env: 'specRetrievalRefreshIntervalMilliseconds'
    },
    specRetrievalGitAuthMode: {
        doc: 'The authentication mode to use when pulling from a GitHub repo',
        format: [SpecRetrievalAuthMode.None, SpecRetrievalAuthMode.Token],
        default: SpecRetrievalAuthMode.None,
        env: 'specRetrievalGitAuthMode'
    },
    specRetrievalGitAuthToken: {
        doc: 'The personal access token to use when using GitHub token authentication',
        format: String,
        default: '',
        env: 'specRetrievalGitAuthToken',
        sensitive: true
    },
    validationPathsPattern: {
        doc: 'The pattern which identifies cached specs to use in validation.',
        format: Array,
        default: ['/specification/**/resource-manager/**/*.json'],
        env: 'validationPathsPattern'
    },
    excludedValidationPathsPattern: {
        doc: 'The pattern which excluded from the validationPathsPattern',
        format: Array,
        default: [
            '**/examples/**/*',
            '**/quickstart-templates/**/*',
            '**/schema/**/*',
            '**/live/**/*',
            '**/wire-format/**/*',
            '**/resource-manager/*/*/*/scenarios/**'
        ],
        env: 'excludedValidationPathsPattern'
    },
    enableExampleGeneration: {
        doc: 'If true example files will not be generated along with each REST calling.',
        format: Boolean,
        default: false,
        env: 'enableExampleGeneration'
    },
    exampleGenerationFolder: {
        doc: 'The folder name used for example generation for each REST calling.',
        format: String,
        default: 'mock',
        env: 'exampleGenerationFolder',
        sensitive: true
    },
    cascadeEnabled: {
        doc:
            'If true resources in different levels should be created orderly and can be deleted cascadingly.',
        format: Boolean,
        default: false,
        env: 'cascadeEnabled'
    }
})
