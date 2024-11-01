import { Logger } from '@azure/logger-js';
import {
  BlobStorageBlob,
  BlobStorageBlockBlob,
  BlobStoragePrefix,
  joinPath
} from '@ts-common/azure-js-dev-tools';
import { InstallationInstructions, InstallationInstructionsOptions } from './installationInstructions';
import { LanguageConfiguration } from './languageConfiguration';
import { SDKAutomationState } from '../sdkAutomationState';
import { SDKRepositoryContext } from '../sdkRepository';

/**
 * The data that describes an SDK repository package.
 */
export interface SDKRepositoryPackageData {
  /**
   * The name of the package.
   */
  readonly name: string;
  /**
   * The relative path to the root of the package folder from the root of the SDK repository.
   */
  readonly relativeFolderPath: string;
  /**
   * The relative path to the root of the extra package folders from the root of the SDK repository.
   */
  extraRelativeFolderPaths: string[];
  /**
   * The current status of creating and uploading this package.
   */
  status: SDKAutomationState;
  /**
   * Message to be shown for this package.
   */
  messages: string[];
  /**
   * Does this package has breaking change.
   */
  hasBreakingChange?: boolean;
  /**
   * The URL of the blob where this SDK repository package's logs will be written to.
   */
  logsBlobUrl: string;
  /**
   * The URLs of the blobs where this SDK repository package's artifacts will be written to.
   */
  artifactBlobUrls?: string[];
  /**
   * The URLs of the generated apiView.
   */
  apiViewUrl?: string;
  /**
   * Used to indicate whether the package should be released as public
   */
  isPrivatePackage: boolean;
  /**
   * The installation instructions for this package.
   */
  installationInstructions?: string;
  /**
   * Lite installation instruction for this package.
   */
  liteInstallationInstruction?: string | undefined;
  /**
   * The URL of the blob where this SDK repository package's installation instructions will be
   * written to.
   */
  installationInstructionsBlobUrl?: string;
  /**
   * The URL to the created generation pull request for this SDK repository package.
   */
  generationPullRequestUrl?: string;
  /**
   * The URL to the diff page of the created generation pull request for this SDK repository package.
   */
  generationPullRequestDiffUrl?: string;
  /**
   * The URL to the integration pull request for this SDK repository package.
   */
  integrationPullRequestUrl?: string;
  /**
   * The files in this package that have changed.
   */
  readonly changedFilePaths: string[];
  /**
   * The name of the generation branch for this package.
   */
  readonly generationBranch: string;
  /**
   * The repository where the SDK's generation pull request and branch will be created. This is the
   * first repository where the automatically generated SDK will appear.
   */
  readonly generationRepository: string;
  /**
   * The URL to this package's generation repository.
   */
  readonly generationRepositoryUrl: string;
  /**
   * The name of the integration branch for this package.
   */
  readonly integrationBranch: string;
  /**
   * The repository where the SDK's integration/staging pull request and branch will be created. The
   * SDK's integration branch and pull request are where merged SDK generation pull requests will be
   * staged before they are merged and published in the main repository.
   */
  readonly integrationRepository: string;
  /**
   * Whether or not this SDK repository package's generation branch should be based off of the
   * integration branch or the main branch.
   */
  readonly useIntegrationBranch: boolean;
  /**
   * The main repository for the SDK. This is where the SDK packages are published from.
   */
  readonly mainRepository: string;
}

/**
 * An SDK repository package.
 */
export class SDKRepositoryPackage {
  /**
   * The file paths to the artifacts that were created as a result of running package commands.
   */
  public readonly artifactFilePaths: string[] = [];

  /**
   * Create a new SDKRepositoryPackage object.
   * @param repositoryFolderPath The rooted path to this package folder.
   * @param language The language that this package is written in.
   * @param logsBlob The blob that this package's logs will be written to.
   * @param specPRIterationPrefix The iteration-specific prefix where this SDK repository's packages
   *  will be uploaded to.
   * @param packagePrefix The specification pull-request-specific prefix where this SDK repository's
   *  packages will be uploaded to.
   * @param logger The logger that will write logs about this SDKRepositoryPackage.
   * @param context The context that this SDKRepositoryPackage will run its operations under.
   * @param data The data related to the SDK repository package.
   */
  constructor(
    public readonly repositoryFolderPath: string,
    private readonly language: LanguageConfiguration,
    public readonly logsBlob: BlobStorageBlob,
    public readonly packagePrefix: BlobStoragePrefix,
    public readonly logger: Logger,
    public readonly context: SDKRepositoryContext,
    public readonly data: SDKRepositoryPackageData,
    public readonly packageIndex: number
  ) {}

  /**
   * Get the rooted folder path of this SDK repository package.
   */
  public getRootedPackageFolderPath(): string {
    return joinPath(this.repositoryFolderPath, this.data.relativeFolderPath);
  }

  /**
   * Create and upload the installation instructions related to this package.
   */
  public async createAndUploadInstallationInstructions(): Promise<void> {
    let instructions: InstallationInstructions | undefined = this.language.installationInstructions;
    let liteInstructions: InstallationInstructions | undefined = this.language.liteInstallationInstruction;
    const options: InstallationInstructionsOptions = {
      packageName: this.data.name,
      artifactUrls: this.data.artifactBlobUrls!,
      generationRepositoryUrl: this.data.generationRepositoryUrl,
      sdkRepositoryGenerationPullRequestHeadBranch: this.data.generationBranch,
      artifactDownloadCommand: this.context.artifactDownloadCommand,
      package: this
    };
    let blobInstructionContent: string = '';
    if (this.language.isPrivatePackage === undefined || this.language.isPrivatePackage === false) {
      if (instructions) {
        await this.logger.logSection(`Creating package installation instructions...`);
        if (typeof instructions === 'function') {
          instructions = await Promise.resolve(instructions(options));
        } else if (typeof instructions !== 'string') {
          instructions = await instructions;
        }
        if (Array.isArray(instructions)) {
          instructions = instructions.join('\n');
        }

        if (instructions) {
          blobInstructionContent = instructions;
          this.data.installationInstructions = instructions;
          const installationInstructionsBlob: BlobStorageBlockBlob = this.packagePrefix.getBlockBlob('instructions.md');
          this.data.installationInstructionsBlobUrl = this.context.getBlobProxyUrl(installationInstructionsBlob);
          await this.logger.logSection(
            `Uploading package installation instructions to ${this.data.installationInstructionsBlobUrl}...`
          );
          await installationInstructionsBlob.setContentsFromString(instructions, { contentType: 'text/plain' });
        }
      }
    }
    if (liteInstructions) {
      await this.logger.logSection(`Creating package LITE installation instructions...`);
      if (typeof liteInstructions === 'function') {
        liteInstructions = await Promise.resolve(liteInstructions(options));
      } else if (typeof liteInstructions !== 'string') {
        liteInstructions = await liteInstructions;
      }
      if (Array.isArray(liteInstructions)) {
        liteInstructions = liteInstructions.join('\n');
      }

      if (liteInstructions) {
        this.data.liteInstallationInstruction = liteInstructions;
        const installationInstructionsBlob: BlobStorageBlockBlob = this.packagePrefix.getBlockBlob(
          'lite_instructions.md'
        );
        this.data.installationInstructionsBlobUrl = this.context.getBlobProxyUrl(installationInstructionsBlob);
        await this.logger.logSection(
          `Uploading package installation instructions to ${this.data.installationInstructionsBlobUrl}...`
        );
        blobInstructionContent = blobInstructionContent + '\n' + liteInstructions;
        await installationInstructionsBlob.setContentsFromString(blobInstructionContent, { contentType: 'text/plain' });
      }
    }
  }

  /**
   * Log an error message and set this SDK repository package's status to "failed".
   * @param errorMessage The error message to log.
   */
  public logError(errorMessage: string): Promise<unknown> {
    this.data.status = 'failed';
    return Promise.all([this.logger.logError(errorMessage), this.context.writeGenerationData()]);
  }
}
