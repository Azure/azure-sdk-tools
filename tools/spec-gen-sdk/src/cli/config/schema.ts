import convict, { Config } from 'convict';
import * as dotenv from 'dotenv';
import { homedir } from 'os';
import path from 'path';

dotenv.config();

export type SDKAutomationCliConfig = {
  env: string;
  workingFolder: string;
  isTriggeredByUP: boolean;
  githubToken: string;
  azureCliArgs: {
    azureDevopsPat: string;
    buildId: string;
  };
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  specRepo: string;
  sdkRepoName: string;
  prNumber: number;
  specCommitSha: string;
  specRepoHttpsUrl: string;
  githubApp: {
    id: number;
    privateKey: string;
  };
  githubCommentAuthorName: string;
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
  localSpecRepoPath: {
    default: '',
    doc: 'Example: /path/to/azure-rest-api-specs',
    env: 'LOCAL_SPEC_REPO_PATH',
    arg: 'local-spec-repo-path',
    format: String
  },
  localSdkRepoPath: {
    default: '',
    doc: 'Example: /path/to/azure-sdk-for-go',
    env: 'LOCAL_SDK_REPO_PATH',
    arg: 'local-sdk-repo-path',
    format: String
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
  specCommitSha: {
    default: '',
    doc: 'Commit sha of the spec pull request',
    env: 'SPEC_COMMIT_SHA',
    arg: 'spec-commit-sha',
    format: String
  },
  specRepoHttpsUrl: {
    default: '',
    doc: 'https://github.com/azure/azure-rest-api-specs',
    env: 'SPEC_REPO_HTTPS_URL',
    arg: 'spec-repo-https-url',
    format: String
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
  }
});
