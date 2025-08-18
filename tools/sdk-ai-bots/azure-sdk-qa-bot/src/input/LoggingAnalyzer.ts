import { exec } from 'child_process';
import { promisify } from 'util';
import { logger } from '../logging/logger.js';
import { RemoteContent } from './RemoteContent.js';

const execAsync = promisify(exec);

export class LoggingAnalyzer {
  private readonly timeout = 600000; // 10 minutes timeout for analysis

  constructor() {}

  public async analyzePipelineLog(pipelineURLs: URL[], meta: object): Promise<RemoteContent[]> {
    return Promise.all(pipelineURLs.map<Promise<RemoteContent>>((url) => this.analyzePipelineLogCore(url, meta)));
  }

  private async analyzePipelineLogCore(pipelineURL: URL, meta: object): Promise<RemoteContent> {
    try {
      logger.info('Starting pipeline log analysis', { pipelineURL: pipelineURL.toString(), meta });
      const result = await this.runPowerShellCommand(pipelineURL.href, meta);
      logger.info('Pipeline log analysis completed successfully', { pipelineURL: pipelineURL.toString(), meta });
      return { text: result, url: pipelineURL };
    } catch (error) {
      logger.error('Failed to analyze pipeline log', {
        error: error.message,
        pipelineURL: pipelineURL.toString(),
        meta,
      });
      return { text: '', url: pipelineURL, error };
    }
  }

  private async runPowerShellCommand(pipelineLink: string, meta: object): Promise<string> {
    try {
      // Run the azsdk analyze command using dotnet from PATH
      const analyzeCommand = `./azsdk-cli/publish-${process.platform}/azsdk azp analyze "${pipelineLink}"`;

      logger.info('Executing analysis command', { analyzeCommand, pipelineLink, meta });

      // Execute the analysis command
      const { stdout, stderr } = await execAsync(analyzeCommand, { cwd: process.cwd(), timeout: this.timeout });

      if (stderr) {
        logger.warn('Analysis command produced warnings', { stderr, meta });
      }

      logger.info('Analysis command executed successfully', { stdout, meta });
      const analyzedResultIndex = stdout.lastIndexOf('Failed Tasks');
      const analyzedResult = stdout.substring(analyzedResultIndex);
      return analyzedResult;
    } catch (error) {
      logger.error('Command execution failed', { error: error.message, pipelineLinkOrBuildId: pipelineLink, meta });
      throw new Error(`Failed to execute analysis command: ${error.message}`);
    }
  }
}
