/**
 * Message prefixes for telemetry in the spec-gen-sdk tool
 */
export enum MessagePrefix {
    ToolError = "SGS-ERR",     // Errors from spec-gen-sdk tool
    ExternalError = "EXT-ERR", // Errors from external scripts
    ConfigError = "CONFIG-ERR", // Errors from configuration
    ToolWarning = "SGS-WARN",  // Warnings from spec-gen-sdk tool
    ExternalWarning = "EXT-WARN", // Warnings from external scripts
    ConfigWarning = "CONFIG-WARN", // Warnings from configuration
  }
  
  /**
   * Formats a message with a consistent prefix for telemetry
   * 
   * @param prefix The prefix to use
   * @param message The message
   * @param details Optional additional details
   * @returns Formatted message with prefix
   */
  export function formatMessage(
    prefix: MessagePrefix,
    message: string,
    details?: string
  ): string {
    const prefixStr = `[${prefix}]`;
  
    if (details) {
        return `${prefixStr} ${message}. ${details}`;
    }
  
    return `${prefixStr} ${message}`;
  }
  
  /**
   * Helper for tool errors
   */
  export function toolError(message: string, details?: string): string {
    return formatMessage(MessagePrefix.ToolError, message, details);
  }
  
  /**
   * Helper for external errors
   */
  export function externalError(message: string, details?: string): string {
    return formatMessage(MessagePrefix.ExternalError, message, details);
  }
  
  /**
   * Helper for configuration errors
   */
  export function configError(message: string, details?: string): string {
    return formatMessage(MessagePrefix.ConfigError, message, details);
  }

  /**
   * Helper for tool warnings
   */
  export function toolWarning(message: string, details?: string): string {
  return formatMessage(MessagePrefix.ToolWarning, message, details);
  }
  
  /**
   * Helper for external warnings
   */
  export function externalWarning(message: string, details?: string): string {
    return formatMessage(MessagePrefix.ExternalWarning, message, details);
  }

  /**
   * Helper for configuration warnings
   */
  export function configWarning(message: string, details?: string): string {
    return formatMessage(MessagePrefix.ConfigWarning, message, details);
  }
  
  /**
   * Check if a message already has a prefix
   */
  export function hasPrefix(message: string): boolean {
    return /^\[(SGS|EXT|CONFIG)-(ERR|WARN)\]/.test(message);
  }
