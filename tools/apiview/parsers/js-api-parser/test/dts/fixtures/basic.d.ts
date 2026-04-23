// basic.d.ts — flat file (no declare module blocks); covers all declaration kinds.

/**
 * Represents an HTTP pipeline policy.
 * @public
 */
export interface RequestPolicy {
  sendRequest(request: HttpRequest): Promise<HttpResponse>;
}

/**
 * An HTTP request.
 */
export interface HttpRequest {
  url: string;
  method: "GET" | "POST" | "PUT" | "DELETE" | "PATCH";
  headers?: Record<string, string>;
  body?: string;
}

/**
 * An HTTP response.
 */
export interface HttpResponse {
  status: number;
  headers: Record<string, string>;
  bodyAsText?: string;
}

/**
 * Base HTTP client class.
 */
export declare class HttpClient {
  readonly defaultTimeout: number;
  sendRequest(request: HttpRequest): Promise<HttpResponse>;
  sendRequest<T>(request: HttpRequest, parser: (body: string) => T): Promise<T>;
}

/**
 * A more capable client that extends the base.
 */
export declare class RetryableHttpClient extends HttpClient {
  constructor(retries: number, timeout?: number);
  readonly maxRetries: number;
  protected retryDelay: number;
  sendRequest(request: HttpRequest): Promise<HttpResponse>;
}

/**
 * Creates a default HTTP client.
 */
export declare function createHttpClient(options?: { timeout?: number }): HttpClient;

/**
 * Known HTTP methods.
 */
export declare enum HttpMethod {
  Get = "GET",
  Post = "POST",
  Put = "PUT",
  Delete = "DELETE",
  Patch = "PATCH",
}

/**
 * A union type for common content-type strings.
 */
export type ContentType =
  | "application/json"
  | "application/xml"
  | "text/plain"
  | "multipart/form-data";

/**
 * Options that all operations share.
 */
export type OperationOptions<T = unknown> = {
  abortSignal?: AbortSignal;
  onResponse?: (response: T) => void;
  requestOptions?: {
    timeout?: number;
    headers?: Record<string, string>;
  };
};

/**
 * Default timeout value in milliseconds.
 */
export declare const DEFAULT_TIMEOUT: number;

/**
 * Namespace for internal utilities.
 */
export declare namespace Internal {
  /**
   * Builds an HTTP request.
   */
  function buildRequest(url: string, method: string): HttpRequest;

  /**
   * Internal retry helper.
   */
  interface RetryOptions {
    maxRetries: number;
    delay: number;
  }
}

/**
 * A callable interface — call signature.
 */
export interface Formatter {
  (value: unknown, format: string): string;
  defaultFormat: string;
}

/**
 * A constructable interface — construct signature.
 */
export interface ClientConstructor {
  new (endpoint: string, credential: string): HttpClient;
  version: string;
}

/**
 * An interface with both call and construct signatures (and a generic).
 */
export interface Factory<T> {
  new <TInput>(opts: TInput): T;
  <TInput>(opts: TInput): T;
}
