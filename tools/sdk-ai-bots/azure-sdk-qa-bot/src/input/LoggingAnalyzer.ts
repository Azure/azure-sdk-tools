import { exec } from 'child_process';
import { promisify } from 'util';
import stripAnsi from 'strip-ansi';
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

  /**
   * Enhanced ANSI escape sequence removal
   * Handles complex escape sequences including colors, formatting, and cursor control
   */
  private cleanAnsiEscapeSequences(text: string): string {
    try {
      // First, try the standard stripAnsi library
      let cleaned = stripAnsi(text);

      // Additional cleanup for more complex ANSI sequences
      cleaned = cleaned
        // Remove ANSI escape sequences that stripAnsi might miss
        .replace(/\x1b\[[0-9;]*[a-zA-Z]/g, '')
        // Remove other control characters
        .replace(/\x1b\[[\d;]*[HfABCDsuKJhl]/g, '')
        // Remove 8-bit color sequences
        .replace(/\x1b\[38;5;\d+m/g, '')
        .replace(/\x1b\[48;5;\d+m/g, '')
        // Remove 24-bit color sequences
        .replace(/\x1b\[38;2;\d+;\d+;\d+m/g, '')
        .replace(/\x1b\[48;2;\d+;\d+;\d+m/g, '')
        // Remove any remaining escape sequences
        .replace(/\x1b\[[0-9;?]*[a-zA-Z]/g, '')
        // Remove other control characters
        .replace(/[\x00-\x1F\x7F-\x9F]/g, '')
        // Replace colored log level indicators with plain text
        .replace(/\[32minfo\[39m/g, 'info')
        .replace(/\[33mwarn\[39m/g, 'warn')
        .replace(/\[31merror\[39m/g, 'error')
        .replace(/\[36mdebug\[39m/g, 'debug')
        .replace(/\[1m\[35msection\[39m\[22m/g, 'section')
        .replace(/\[1m\[35mendsection\[39m\[22m/g, 'endsection')
        .replace(/\[1m\[36mcommand\[39m\[22m/g, 'command')
        .replace(/\[4m\[32mcmdout\[39m\[24m/g, 'cmdout')
        .replace(/\[4m\[33mcmderr\[39m\[24m/g, 'cmderr')
        // Clean up multiple spaces
        .replace(/\s+/g, ' ')
        // Remove leading/trailing whitespace from each line
        .split('\n')
        .map((line) => line.trim())
        .join('\n')
        // Remove empty lines
        .replace(/\n\s*\n/g, '\n')
        .trim();

      return cleaned;
    } catch (error) {
      logger.warn('Failed to clean ANSI sequences, returning original text', { error: error.message });
      return text;
    }
  }

  private async runPowerShellCommand(pipelineLink: string, meta: object): Promise<string> {
    try {
      // Run the azsdk analyze command using dotnet from PATH
      const exeSuffix = process.platform === 'win32' ? '.exe' : '';
      const shell = process.platform === 'win32' ? 'powershell.exe' : 'bash';
      const analyzeCommand = `./azsdk-cli/publish-${process.platform}/azsdk${exeSuffix} azp analyze "${pipelineLink}"`;

      logger.info('Executing analysis command', { analyzeCommand, pipelineLink, meta });

      // Execute the analysis command
      const { stdout, stderr } = await execAsync(analyzeCommand, { timeout: this.timeout, shell });

      // Use enhanced cleaning for both stdout and stderr
      const cleanStdOut = this.cleanAnsiEscapeSequences(stdout);
      const cleanStderr = this.cleanAnsiEscapeSequences(stderr);

      if (stderr) {
        logger.warn('Analysis command produced warnings', { stderr: cleanStderr, meta });
      }

      logger.info('Analysis command executed successfully', { meta });
      const analyzedResultIndex = cleanStdOut.lastIndexOf('Failed Tasks');
      const analyzedResult = analyzedResultIndex >= 0 ? cleanStdOut.substring(analyzedResultIndex) : cleanStdOut;

      return analyzedResult;
    } catch (error) {
      logger.error('Command execution failed', { error: error.message, pipelineLinkOrBuildId: pipelineLink, meta });
      throw new Error(`Failed to execute analysis command: ${error.message}`);
    }
  }
}
