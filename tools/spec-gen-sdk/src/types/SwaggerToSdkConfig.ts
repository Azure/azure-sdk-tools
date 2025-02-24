import { requireJsonc } from '../utils/requireJsonc';
import { getTypeTransformer } from './validator';

export const swaggerToSdkConfigSchema = requireJsonc(__dirname + '/SwaggerToSdkConfigSchema.json');

export type RunLogFilterOptions = RegExp | boolean;

export type RunLogOptions = {
  showInComment?: RunLogFilterOptions;
  scriptError?: RunLogFilterOptions;
  scriptWarning?: RunLogFilterOptions;
};

export type RunOptions = {
  path: string;
  envs?: string[];
  logPrefix?: string;
  stdout?: RunLogOptions;
  stderr?: RunLogOptions;
  exitCode?: {
    showInComment: boolean;
    result: 'error' | 'warning' | 'ignore';
  };
};

export type SwaggerToSdkConfig = {
  advancedOptions: {
    createSdkPullRequests: boolean;
    closeIntegrationPR: boolean;
    draftIntegrationPR: boolean;
    draftGenerationPR: boolean;
    generationCallMode?: 'one-per-config' | 'one-for-all-configs';
    cloneDir?: string;
  };
  initOptions?: {
    initScript?: RunOptions;
  };
  generateOptions: {
    generateScript?: RunOptions;
    preprocessDryRunGetPackageName: boolean;
    parseGenerateOutput: boolean;
  };
  packageOptions: {
    packageFolderFromFileSearch:
      | {
          searchRegex: RegExp;
          packageNamePrefix?: string;
        }
      | false;
    buildScript?: RunOptions;
    changelogScript?: RunOptions & {
      breakingChangeDetect?: RunLogFilterOptions;
    };
    breakingChangeLabel?: string;
    breakingChangesLabel?: string;
  };
  artifactOptions: {
    artifactPathFromFileSearch?:
      | {
          searchRegex: RegExp;
          searchFolder?: string;
        }
      | false;
    installInstructionScript?: RunOptions;
  };
};

export const getSwaggerToSdkConfig = getTypeTransformer<SwaggerToSdkConfig>(
  swaggerToSdkConfigSchema,
  'SwaggerToSdkConfig'
);
