import convict, { Config } from 'convict';
import * as dotenv from 'dotenv';
import { homedir } from 'os';
import path from 'path';

dotenv.config();

export type SDKAutomationCliConfig = {
  env: string;
  workingFolder: string;
  executionMode: string;
  isTriggeredByUP: boolean;
  githubToken: string;
  azureCliArgs: {
    azureDevopsPat: string;
    buildId: string;
  };
  specRepo: string;
  sdkRepoName: string;
  prNumber: number;
  githubApp: {
    id: number;
    privateKey: string;
  };
  githubCommentAuthorName: string;
  blobStorage: {
    name: string;
    prefix: string;
    downloadCommand: string;
    isPublic: boolean;
  };
  testRunId?: string;
};

const emptyValidator = (value?: string): void => {
  if (value === undefined || value === '') {
    throw new Error(`ConfigError: spec repo cannot be empty or undefined. Please set the 'SPEC_REPO' environment variable or pass in the value when run the tool.`);
  }
};

export const configurationSchema: Config<SDKAutomationCliConfig> = convict<SDKAutomationCliConfig>({
  env: {
    default: '',
    env: 'ENV',
    arg: 'env',
    format: String
  },
  workingFolder: {
    default: path.join(homedir(), '.sdkauto'),
    env: 'WORKING_FOLDER',
    arg: 'working-folder',
    format: String
  },
  executionMode: {
    default: 'SDK_GEN',
    env: 'EXECUTION_MODE',
    arg: 'execution-mode',
    format: ['SDK_GEN', 'SDK_FILTER']
  },
  isTriggeredByUP: {
    default: false,
    env: 'IS_TRIGGERED_BY_UP',
    arg: 'is-triggered-by-up',
    format: Boolean
  },
  githubToken: {
    default: '',
    doc: 'Generate from https://github.com/settings/tokens/new. Keep it empty if want to use github app',
    env: 'GITHUB_TOKEN',
    arg: 'github-token',
    format: String
  },
  azureCliArgs: {
    azureDevopsPat: {
      default: '',
      doc: 'Used for az cli command',
      env: 'AZURE_DEVOPS_EXT_PAT',
      arg: 'azure-devops-ext-pat',
      format: String
    },
    buildId: {
      default: '1',
      doc: 'Used for az cli package version',
      env: 'BUILD_ID',
      format: String
    }
  },
  specRepo: {
    default: '',
    doc: 'Example: Azure/azure-rest-api-specs',
    env: 'SPEC_REPO',
    arg: 'spec-repo',
    format: emptyValidator
  },
  sdkRepoName: {
    default: '',
    doc: 'Example: azure-sdk-for-go',
    env: 'SDK_REPO_NAME',
    arg: 'sdk',
    format: String
  },
  prNumber: {
    default: 0,
    doc: 'Pull Request Number',
    env: 'PR_NUMBER',
    arg: 'pr',
    format: Number
  },
  githubApp: {
    id: {
      default: 0,
      env: 'GITHUBAPP_ID',
      arg: 'app-id',
      format: Number
    },
    privateKey: {
      default: '',
      env: 'GITHUBAPP_PRIVATE_KEY',
      arg: 'app-private-key',
      format: String
    }
  },
  githubCommentAuthorName: {
    default: 'openapi-bot[bot]',
    doc: 'Comment author name shown on github comment. Add [bot] if githubApp is used',
    env: 'GITHUB_COMMENT_AUTHOR_NAME',
    arg: 'comment-author-name',
    format: String
  },
  blobStorage: {
    name: {
      default: '',
      doc: 'The blob storage name that should be used to store all data that is created as a result of the automation.',
      env: 'BLOB_STORAGE_NAME',
      arg: 'blob-name',
      format: String
    },
    prefix: {
      default: 'sdkautomation-pipeline',
      doc:
        'The blob storage prefix that should be used to store all data that is created as a result of the automation.',
      env: 'BLOB_STORAGE_PREFIX',
      arg: 'blob-prefix',
      format: String
    },
    isPublic: {
      default: true,
      format: Boolean,
      doc: '',
      env: 'BLOB_STORAGE_IS_PUBLIC',
      arg: 'blob-is-public'
    },
    downloadCommand: {
      default: 'az rest --resource <client_id> -u {URL} -o {FILENAME}',
      doc: '',
      env: 'BLOB_DOWNLOAD_COMMAND',
      arg: 'blob-download-command',
      format: String
    }
  },
  testRunId: {
    default: '',
    doc: 'Used in integration test only. See ./integrationtest/utils.ts#getRunIdPrefix()',
    env: 'TEST_RUN_ID',
    arg: 'test-run-id',
    format: String
  }
});
