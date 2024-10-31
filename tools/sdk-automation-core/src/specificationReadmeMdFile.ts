import { Logger } from '@azure/logger-js';
import {
  findSwaggerToSDKConfiguration,
  getRepository,
  getRepositoryFullName,
  ReadmeMdSwaggerToSDKConfiguration,
  Repository,
  RepositoryConfiguration,
  joinPath
} from '@ts-common/azure-js-dev-tools';
import { readFileSync } from 'fs';

/**
 * A readme.md file that is used as an AutoRest configuration file within an OpenAPI specification
 * repository.
 */
export class SpecificationReadmeMdFile {
  /**
   * The URL to the contents of this file.
   */
  public readonly contentsPath: string;
  /**
   * Create a new SpecificationReadmeMdFile with the provided details.
   * @param httpClient The HTTPClient to use to get this file's contents.
   * @param logger The Logger that will be used to write logs to.
   * @param relativeFilePath The path to this file relative to the root of the repository.
   */
  constructor(specRepoFolder: string, private readonly logger: Logger, public readonly relativeFilePath: string) {
    this.contentsPath = joinPath(specRepoFolder, relativeFilePath);
  }

  /**
   * Get the contents of this file, if it exists.
   * @returns The contents of this file or undefined if the file doesn't exist.
   */
  public async getContents(): Promise<string | undefined> {
    await this.logger.logSection(`Getting file contents for "${this.contentsPath}"...`);
    try {
      const buffer = readFileSync(this.contentsPath);
      return buffer.toString();
    } catch (e) {
      await this.logger.logWarning(`File ${this.contentsPath} not found`);
      return undefined;
    }
  }

  /**
   * Get the swagger-to-sdk configuration section from this file.
   */
  public async getSwaggerToSDKConfiguration(): Promise<ReadmeMdSwaggerToSDKConfiguration | undefined> {
    let result: ReadmeMdSwaggerToSDKConfiguration | undefined;
    const contents: string | undefined = await this.getContents();
    if (!contents) {
      await this.logger.logError(`Merged readme.md response body is empty.`);
    } else {
      result = findSwaggerToSDKConfiguration(contents);
      if (!result) {
        await this.logger.logError(`No SwaggerToSDK configuration YAML block found in the merged readme.md.`);
      }
    }
    return result;
  }

  /**
   * Get the repositories that have been configured for SDK Automation to be run on them for this
   * specification readme.md file.
   */
  public async getSwaggerToSDKRepositoryConfigurations(): Promise<RepositoryConfiguration[] | undefined> {
    let result: RepositoryConfiguration[] | undefined;
    const swaggerToSDKConfiguration:
      | ReadmeMdSwaggerToSDKConfiguration
      | undefined = await this.getSwaggerToSDKConfiguration();
    if (swaggerToSDKConfiguration) {
      result = [];
      await this.logger.logInfo(`Found ${swaggerToSDKConfiguration.repositories.length} requested SDK repositories:`);
      for (const requestedRepository of swaggerToSDKConfiguration.repositories) {
        const repository: Repository = getRepository(requestedRepository.repo);
        requestedRepository.repo = getRepositoryFullName(repository);
        await this.logger.logInfo(`  ${requestedRepository.repo}`);
        result.push(requestedRepository);
      }
    }
    return result;
  }
}
