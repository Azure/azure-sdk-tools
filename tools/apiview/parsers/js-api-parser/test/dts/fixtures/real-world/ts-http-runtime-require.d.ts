/// <reference lib="es2020" />
// @typespec/ts-http-runtime - Public API Surface
// Graphed by PublicApiGraphEngine.TypeScript

// --- @typespec/ts-http-runtime ---

declare module "@typespec/ts-http-runtime" {
      /** This error is thrown when an asynchronous operation has been aborted. */
      export class AbortError extends Error {
          constructor(message?: string);
      }


      // Reachable via: RetryInformation → RestError
      // Reachable via: RetryModifiers → RestError
      /** A custom error type for failed pipeline requests. */
      export class RestError extends Error {
          readonly REQUEST_SEND_ERROR: string;
          readonly PARSE_ERROR: string;
          code?: string;
          statusCode?: number;
          request?: PipelineRequest;
          response?: PipelineResponse;
          details?: unknown;
          constructor(message: string, options?: RestErrorOptions);
      }


      // Reachable via: OAuth2TokenCredential → GetOAuth2TokenOptions
      /** Options used when creating and sending get OAuth 2 requests for this operation. */
      export interface GetOAuth2TokenOptions {
          abortSignal?: AbortSignal;
      }


      // Reachable via: BearerTokenCredential → GetBearerTokenOptions
      /** Options used when creating and sending get bearer token requests for this operation. */
      export interface GetBearerTokenOptions {
          abortSignal?: AbortSignal;
      }


      // Reachable via: ClientCredential → OAuth2TokenCredential
      /** Credential for OAuth2 authentication flows. */
      export interface OAuth2TokenCredential<TFlows extends OAuth2Flow> {
          getOAuth2Token(flows: TFlows[], options?: GetOAuth2TokenOptions): Promise<string>;
      }


      // Reachable via: ClientCredential → BearerTokenCredential
      /** Credential for Bearer token authentication. */
      export interface BearerTokenCredential {
          getBearerToken(options?: GetBearerTokenOptions): Promise<string>;
      }


      // Reachable via: ClientCredential → BasicCredential
      /** Credential for HTTP Basic authentication. */
      export interface BasicCredential {
          username: string;
          password: string;
      }


      // Reachable via: ClientCredential → ApiKeyCredential
      /** Credential for API Key authentication. */
      export interface ApiKeyCredential {
          key: string;
      }


      // Reachable via: OAuth2Flow → AuthorizationCodeFlow
      /** Represents OAuth2 Authorization Code flow configuration. */
      export interface AuthorizationCodeFlow {
          kind: "authorizationCode";
          authorizationUrl: string;
          tokenUrl: string;
          refreshUrl?: string;
          scopes?: string[];
      }


      // Reachable via: OAuth2Flow → ClientCredentialsFlow
      /** Represents OAuth2 Client Credentials flow configuration. */
      export interface ClientCredentialsFlow {
          kind: "clientCredentials";
          tokenUrl: string;
          refreshUrl?: string[];
          scopes?: string[];
      }


      // Reachable via: OAuth2Flow → ImplicitFlow
      /** Represents OAuth2 Implicit flow configuration. */
      export interface ImplicitFlow {
          kind: "implicit";
          authorizationUrl: string;
          refreshUrl?: string;
          scopes?: string[];
      }


      // Reachable via: OAuth2Flow → PasswordFlow
      /** Represents OAuth2 Password flow configuration. */
      export interface PasswordFlow {
          kind: "password";
          tokenUrl: string;
          refreshUrl?: string;
          scopes?: string[];
      }


      // Reachable via: AuthScheme → BasicAuthScheme
      /** Represents HTTP Basic authentication scheme. */
      export interface BasicAuthScheme {
          kind: "http";
          scheme: "basic";
      }


      // Reachable via: AuthScheme → BearerAuthScheme
      /** Represents HTTP Bearer authentication scheme. */
      export interface BearerAuthScheme {
          kind: "http";
          scheme: "bearer";
      }


      // Reachable via: AuthScheme → NoAuthAuthScheme
      /** Represents an endpoint or operation that requires no authentication. */
      export interface NoAuthAuthScheme {
          kind: "noAuth";
      }


      // Reachable via: AuthScheme → ApiKeyAuthScheme
      /** Represents API Key authentication scheme. */
      export interface ApiKeyAuthScheme {
          kind: "apiKey";
          apiKeyLocation: "query" | "header" | "cookie";
          name: string;
      }


      // Reachable via: AuthScheme → OAuth2AuthScheme
      /** Represents OAuth2 authentication scheme with specified flows */
      export interface OAuth2AuthScheme<TFlows extends OAuth2Flow[]> {
          kind: "oauth2";
          flows: TFlows;
      }


      // Reachable via: OperationOptions → FullOperationResponse
      // Reachable via: RawResponseCallback → FullOperationResponse
      /** Wrapper object for http request and response. Deserialized object is stored in */
      export interface FullOperationResponse extends PipelineResponse {
          rawHeaders?: RawHttpHeaders;
          parsedBody?: RequestBodyType;
          request: PipelineRequest;
      }


      /** The base options type for all operations. */
      export interface OperationOptions {
          abortSignal?: AbortSignal;
          requestOptions?: OperationRequestOptions;
          onResponse?: RawResponseCallback;
      }


      // Reachable via: OperationOptions → OperationRequestOptions
      /** Options used when creating and sending HTTP requests for this operation. */
      export interface OperationRequestOptions {
          headers?: RawHttpHeadersInput;
          timeout?: number;
          allowInsecureConnection?: boolean;
          skipUrlEncoding?: boolean;
          onUploadProgress(progress: TransferProgressEvent): void;
          onDownloadProgress(progress: TransferProgressEvent): void;
      }


      /** Shape of a Rest Level Client */
      export interface Client {
          pipeline: Pipeline;
          path: Function;
          pathUnchecked: PathUnchecked;
      }


      // Reachable via: Client → ResourceMethods
      // Reachable via: PathUnchecked → ResourceMethods
      /** Defines the methods that can be called on a resource */
      export interface ResourceMethods<TResponse = PromiseLike<PathUncheckedResponse>> {
          get(options?: RequestParameters): TResponse;
          post(options?: RequestParameters): TResponse;
          put(options?: RequestParameters): TResponse;
          patch(options?: RequestParameters): TResponse;
          delete(options?: RequestParameters): TResponse;
          head(options?: RequestParameters): TResponse;
          options(options?: RequestParameters): TResponse;
          trace(options?: RequestParameters): TResponse;
      }


      // Reachable via: ClientOptions → AdditionalPolicyConfig
      /** Used to configure additional policies added to the pipeline at construction. */
      export interface AdditionalPolicyConfig {
          policy: PipelinePolicy;
          position: "perCall" | "perRetry";
      }


      // Reachable via: PathParameters → PathParameterWithOptions
      /** An object that can be passed as a path parameter, allowing for additional options to be set relating to how the param... */
      export interface PathParameterWithOptions {
          value: string | number;
          allowReserved?: boolean;
      }


      // Reachable via: ClientOptions → PipelineOptions
      /** Defines options that are used to configure the HTTP pipeline for */
      export interface PipelineOptions {
          retryOptions?: PipelineRetryOptions;
          proxyOptions?: ProxySettings;
          agent?: Agent;
          tlsOptions?: TlsSettings;
          redirectOptions?: RedirectPolicyOptions;
          userAgentOptions?: UserAgentPolicyOptions;
          telemetryOptions?: TelemetryOptions;
      }


      // Reachable via: PipelineOptions → TelemetryOptions
      /** Defines options that are used to configure common telemetry and tracing info */
      export interface TelemetryOptions {
          clientRequestIdHeaderName?: string;
      }


      // Reachable via: BodyPart → HttpHeaders
      // Reachable via: PipelineRequest → HttpHeaders
      // Reachable via: PipelineRequestOptions → HttpHeaders
      // Reachable via: PipelineResponse → HttpHeaders
      /** Represents a set of HTTP headers on a request/response. */
      export interface HttpHeaders extends Iterable<[string, string]> {
          get(name: string): string | undefined;
          has(name: string): boolean;
          set(name: string, value: string | number | boolean): void;
          delete(name: string): void;
          toJSON(options?: {
              preserveCase?: boolean;
          }): RawHttpHeaders;
      }


      // Reachable via: MultipartRequestBody → BodyPart
      /** A part of the request body in a multipart request. */
      export interface BodyPart {
          headers: HttpHeaders;
          body: ((() => WebReadableStream<Uint8Array>) | (() => NodeReadableStream)) | WebReadableStream<Uint8Array> | NodeReadableStream | Uint8Array | Blob;
      }


      // Reachable via: PipelineRequest → MultipartRequestBody
      // Reachable via: PipelineRequestOptions → MultipartRequestBody
      /** A request body consisting of multiple parts. */
      export interface MultipartRequestBody {
          parts: BodyPart[];
          boundary?: string;
      }


      // Reachable via: PipelineOptions → Agent
      // Reachable via: PipelineRequest → Agent
      /** An interface compatible with NodeJS's `http.Agent`. */
      export interface Agent {
          maxFreeSockets: number;
          maxSockets: number;
          requests: unknown;
          sockets: unknown;
          destroy(): void;
      }


      // Reachable via: FullOperationResponse → PipelineRequest
      // Reachable via: HttpResponse → PipelineRequest
      // Reachable via: Pipeline → PipelineRequest
      // Reachable via: PipelinePolicy → PipelineRequest
      // Reachable via: PipelineResponse → PipelineRequest
      // Reachable via: RestError → PipelineRequest
      // Reachable via: RestErrorOptions → PipelineRequest
      // Reachable via: SendRequest → PipelineRequest
      /** Metadata about a request being made by the pipeline. */
      export interface PipelineRequest {
          authSchemes?: AuthScheme[];
          url: string;
          method: HttpMethods;
          headers: HttpHeaders;
          timeout: number;
          withCredentials: boolean;
          requestId: string;
          body?: RequestBodyType;
          multipartBody?: MultipartRequestBody;
          formData?: FormDataMap;
          streamResponseStatusCodes?: Set<number>;
          proxySettings?: ProxySettings;
          disableKeepAlive?: boolean;
          abortSignal?: AbortSignal;
          allowInsecureConnection?: boolean;
          agent?: Agent;
          enableBrowserStreams?: boolean;
          tlsSettings?: TlsSettings;
          requestOverrides?: Record<string, unknown>;
          onUploadProgress(progress: TransferProgressEvent): void;
          onDownloadProgress(progress: TransferProgressEvent): void;
      }


      // Reachable via: FullOperationResponse → PipelineResponse
      // Reachable via: Pipeline → PipelineResponse
      // Reachable via: PipelinePolicy → PipelineResponse
      // Reachable via: RestError → PipelineResponse
      // Reachable via: RestErrorOptions → PipelineResponse
      // Reachable via: RetryInformation → PipelineResponse
      // Reachable via: SendRequest → PipelineResponse
      /** Metadata about a response received by the pipeline. */
      export interface PipelineResponse {
          request: PipelineRequest;
          status: number;
          headers: HttpHeaders;
          bodyAsText?: string | null;
          blobBody?: Promise<Blob>;
          browserStreamBody?: WebReadableStream<Uint8Array>;
          readableStreamBody?: NodeReadableStream;
      }


      // Reachable via: ClientOptions → HttpClient
      // Reachable via: Pipeline → HttpClient
      /** The required interface for a client that makes HTTP requests */
      export interface HttpClient {
          sendRequest: SendRequest;
      }


      // Reachable via: PipelineOptions → ProxySettings
      // Reachable via: PipelineRequest → ProxySettings
      // Reachable via: PipelineRequestOptions → ProxySettings
      /** Options to configure a proxy for outgoing requests (Node.js only). */
      export interface ProxySettings {
          host: string;
          port: number;
          username?: string;
          password?: string;
      }


      // Reachable via: DefaultRetryPolicyOptions → PipelineRetryOptions
      // Reachable via: PipelineOptions → PipelineRetryOptions
      /** Options that control how to retry failed requests. */
      export interface PipelineRetryOptions {
          maxRetries?: number;
          retryDelayInMs?: number;
          maxRetryDelayInMs?: number;
      }


      // Reachable via: PipelineOptions → TlsSettings
      // Reachable via: PipelineRequest → TlsSettings
      /** Represents a certificate for TLS authentication. */
      export interface TlsSettings {
          ca?: string | NodeBuffer | Array<string | NodeBuffer> | undefined;
          cert?: string | NodeBuffer | Array<string | NodeBuffer> | undefined;
          key?: string | NodeBuffer | Array<NodeBuffer | KeyObject> | undefined;
          passphrase?: string | undefined;
          pfx?: string | NodeBuffer | Array<string | NodeBuffer | PxfObject> | undefined;
      }


      // Reachable via: PipelineOptions → KeyObject
      // Reachable via: TlsSettings → KeyObject
      /** An interface compatible with NodeJS's `tls.KeyObject`. */
      export interface KeyObject {
          pem: string | NodeBuffer;
          passphrase?: string | undefined;
      }


      // Reachable via: PipelineOptions → PxfObject
      // Reachable via: TlsSettings → PxfObject
      /** An interface compatible with NodeJS's `tls.PxfObject`. */
      export interface PxfObject {
          buf: string | NodeBuffer;
          passphrase?: string | undefined;
      }


      // Reachable via: LogPolicyOptions → Debugger
      // Reachable via: TypeSpecRuntimeClientLogger → Debugger
      // Reachable via: TypeSpecRuntimeLogger → Debugger
      /** A log function that can be dynamically enabled and redirected. */
      export interface Debugger {
          enabled: boolean;
          namespace: string;
          destroy(): boolean;
          log(args?: any[]): void;
          extend(namespace: string): Debugger;
      }


      // Reachable via: RetryPolicyOptions → TypeSpecRuntimeLogger
      // Reachable via: RetryStrategy → TypeSpecRuntimeLogger
      /** Defines the methods available on the SDK-facing logger. */
      export interface TypeSpecRuntimeLogger {
          error: Debugger;
          warning: Debugger;
          info: Debugger;
          verbose: Debugger;
      }


      // Reachable via: Pipeline → AddPolicyOptions
      /** Options when adding a policy to the pipeline. */
      export interface AddPolicyOptions {
          beforePolicies?: string[];
          afterPolicies?: string[];
          afterPhase?: PipelinePhase;
          phase?: PipelinePhase;
      }


      // Reachable via: AdditionalPolicyConfig → PipelinePolicy
      // Reachable via: Pipeline → PipelinePolicy
      /** A pipeline policy manipulates a request as it travels through the pipeline. */
      export interface PipelinePolicy {
          name: string;
          sendRequest(request: PipelineRequest, next: SendRequest): Promise<PipelineResponse>;
      }


      // Reachable via: Client → Pipeline
      // Reachable via: ClientOptions → Pipeline
      /** Represents a pipeline for making a HTTP request to a URL. */
      export interface Pipeline {
          addPolicy(policy: PipelinePolicy, options?: AddPolicyOptions): void;
          removePolicy(options: {
              name?: string;
              phase?: PipelinePhase;
          }): PipelinePolicy[];
          sendRequest(httpClient: HttpClient, request: PipelineRequest): Promise<PipelineResponse>;
          getOrderedPolicies(): PipelinePolicy[];
          clone(): Pipeline;
      }


      /** Settings to initialize a request. */
      export interface PipelineRequestOptions {
          url: string;
          method?: HttpMethods;
          headers?: HttpHeaders;
          timeout?: number;
          withCredentials?: boolean;
          requestId?: string;
          body?: RequestBodyType;
          multipartBody?: MultipartRequestBody;
          formData?: FormDataMap;
          streamResponseStatusCodes?: Set<number>;
          enableBrowserStreams?: boolean;
          proxySettings?: ProxySettings;
          disableKeepAlive?: boolean;
          abortSignal?: AbortSignal;
          allowInsecureConnection?: boolean;
          authSchemes?: AuthScheme[];
          requestOverrides?: Record<string, unknown>;
          onUploadProgress(progress: TransferProgressEvent): void;
          onDownloadProgress(progress: TransferProgressEvent): void;
      }


      // Reachable via: ClientOptions → LogPolicyOptions
      /** Options to configure the logPolicy. */
      export interface LogPolicyOptions {
          additionalAllowedHeaderNames?: string[];
          additionalAllowedQueryParameters?: string[];
          logger?: Debugger;
      }


      // Reachable via: PipelineOptions → RedirectPolicyOptions
      /** Options for how redirect responses are handled. */
      export interface RedirectPolicyOptions {
          maxRetries?: number;
          allowCrossOriginRedirects?: boolean;
      }


      /** Options that control how to retry failed requests. */
      export interface SystemErrorRetryPolicyOptions {
          maxRetries?: number;
          retryDelayInMs?: number;
          maxRetryDelayInMs?: number;
      }


      /** Options that control how to retry failed requests. */
      export interface ThrottlingRetryPolicyOptions {
          maxRetries?: number;
      }


      // Reachable via: PipelineOptions → UserAgentPolicyOptions
      /** Options for adding user agent details to outgoing requests. */
      export interface UserAgentPolicyOptions {
          userAgentPrefix?: string;
      }


      // Reachable via: RestError → RestErrorOptions
      /** The options supported by RestError. */
      export interface RestErrorOptions {
          code?: string;
          statusCode?: number;
          request?: PipelineRequest;
          response?: PipelineResponse;
      }


      // Reachable via: ClientOptions → ClientCredential
      /** Union type of all supported authentication credentials. */
      export type ClientCredential = OAuth2TokenCredential<OAuth2Flow> | BearerTokenCredential | BasicCredential | ApiKeyCredential;


      // Reachable via: AuthScheme → OAuth2Flow
      // Reachable via: ClientCredential → OAuth2Flow
      // Reachable via: OAuth2AuthScheme → OAuth2Flow
      // Reachable via: OAuth2TokenCredential → OAuth2Flow
      /** Union type of all supported OAuth2 flows */
      export type OAuth2Flow = AuthorizationCodeFlow | ClientCredentialsFlow | ImplicitFlow | PasswordFlow;


      // Reachable via: ClientOptions → AuthScheme
      // Reachable via: PipelineRequest → AuthScheme
      // Reachable via: PipelineRequestOptions → AuthScheme
      /** Union type of all supported authentication schemes */
      export type AuthScheme = BasicAuthScheme | BearerAuthScheme | NoAuthAuthScheme | ApiKeyAuthScheme | OAuth2AuthScheme<OAuth2Flow[]>;


      // Reachable via: Client → RequestParameters
      // Reachable via: ResourceMethods → RequestParameters
      /** Shape of the default request parameters, this may be overridden by the specific */
      export type RequestParameters = {
          /**
           * Headers to send along with the request
           */
          headers?: RawHttpHeadersInput;
          /**
           * Sets the accept header to send to the service
           * defaults to 'application/json'. If also a header "accept" is set
           * this property will take precedence.
           */
          accept?: string;
          /**
           * Body to send with the request
           */
          body?: unknown;
          /**
           * Query parameters to send with the request
           */
          queryParameters?: Record<string, unknown>;
          /**
           * Set an explicit content-type to send with the request. If also a header "content-type" is set
           * this property will take precedence.
           */
          contentType?: string;
          /** Set to true if the request is sent over HTTP instead of HTTPS */
          allowInsecureConnection?: boolean;
          /** Set to true if you want to skip encoding the path parameters */
          skipUrlEncoding?: boolean;
          /**
           * Path parameters for custom the base url
           */
          pathParameters?: Record<string, any>;
          /**
           * The number of milliseconds a request can take before automatically being terminated.
           */
          timeout?: number;
          /**
           * Callback which fires upon upload progress.
           */
          onUploadProgress?: (progress: TransferProgressEvent) => void;
          /**
           * Callback which fires upon download progress.
           */
          onDownloadProgress?: (progress: TransferProgressEvent) => void;
          /**
           * The signal which can be used to abort requests.
           */
          abortSignal?: AbortSignal;
          /**
           * A function to be called each time a response is received from the server
           * while performing the requested operation.
           * May be called multiple times.
           */
          onResponse?: RawResponseCallback;
      };


      // Reachable via: Client → RawResponseCallback
      // Reachable via: OperationOptions → RawResponseCallback
      // Reachable via: RequestParameters → RawResponseCallback
      /** A function to be called each time a response is received from the server */
      export type RawResponseCallback = (rawResponse: FullOperationResponse, error?: unknown) => void;


      // Reachable via: Client → PathUncheckedResponse
      // Reachable via: StreamableMethod → PathUncheckedResponse
      /** Type to use with pathUnchecked, overrides the body type to any to allow flexibility */
      export type PathUncheckedResponse = HttpResponse & {
          body: any;
      };


      // Reachable via: Client → HttpNodeStreamResponse
      // Reachable via: StreamableMethod → HttpNodeStreamResponse
      /** Http Response which body is a NodeJS stream object */
      export type HttpNodeStreamResponse = HttpResponse & {
          /**
           * Streamable body
           */
          body?: NodeReadableStream;
      };


      // Reachable via: Client → HttpBrowserStreamResponse
      // Reachable via: StreamableMethod → HttpBrowserStreamResponse
      /** Http Response which body is a NodeJS stream object */
      export type HttpBrowserStreamResponse = HttpResponse & {
          /**
           * Streamable body
           */
          body?: WebReadableStream<Uint8Array>;
      };


      // Reachable via: Client → StreamableMethod
      // Reachable via: PathUnchecked → StreamableMethod
      /** Defines the type for a method that supports getting the response body as */
      export type StreamableMethod<TResponse = PathUncheckedResponse> = PromiseLike<TResponse> & {
          /**
           * Returns the response body as a NodeJS stream. Only available in Node-like environments.
           */
          asNodeStream: () => Promise<HttpNodeStreamResponse>;
          /**
           * Returns the response body as a browser (Web) stream. Only available in the browser. If you require a Web Stream of the response in Node, consider using the
           * `Readable.toWeb` Node API on the result of `asNodeStream`.
           */
          asBrowserStream: () => Promise<HttpBrowserStreamResponse>;
      };


      // Reachable via: Client → PathUnchecked
      /** Defines the signature for pathUnchecked. */
      export type PathUnchecked = <TPath extends string>(path: TPath, ...args: PathParameters<TPath>) => ResourceMethods<StreamableMethod>;


      /** General options that a Rest Level Client can take */
      export type ClientOptions = PipelineOptions & {
          /**
           * List of authentication schemes supported by the client.
           * These schemes define how the client can authenticate requests.
           */
          authSchemes?: AuthScheme[];
          /**
           * The credential used to authenticate requests.
           * Must be compatible with one of the specified authentication schemes.
           */
          credential?: ClientCredential;
          /**
           * Endpoint for the client
           */
          endpoint?: string;
          /**
           * Options for setting a custom apiVersion.
           */
          apiVersion?: string;
          /**
           * Option to allow calling http (insecure) endpoints
           */
          allowInsecureConnection?: boolean;
          /**
           * Additional policies to include in the HTTP pipeline.
           */
          additionalPolicies?: AdditionalPolicyConfig[];
          /**
           * Specify a custom HttpClient when making requests.
           */
          httpClient?: HttpClient;
          /**
           * Options to configure request/response logging.
           */
          loggingOptions?: LogPolicyOptions;
          /**
           * Pipeline to use for the client. If not provided, a default pipeline will be created using the options provided.
           * Use with caution -- when setting this option, all client options that are used in the creation of the default pipeline
           * will be ignored.
           */
          pipeline?: Pipeline;
      };


      // Reachable via: Client → HttpResponse
      // Reachable via: HttpBrowserStreamResponse → HttpResponse
      // Reachable via: HttpNodeStreamResponse → HttpResponse
      // Reachable via: PathUncheckedResponse → HttpResponse
      /** Represents the shape of an HttpResponse */
      export type HttpResponse = {
          /**
           * The request that generated this response.
           */
          request: PipelineRequest;
          /**
           * The HTTP response headers.
           */
          headers: RawHttpHeaders;
          /**
           * Parsed body
           */
          body: unknown;
          /**
           * The HTTP status code of the response.
           */
          status: string;
      };


      // Reachable via: Client → PathParameters
      // Reachable via: PathUnchecked → PathParameters
      /** Helper type used to detect parameters in a path template */
      export type PathParameters<TRoute extends string> = TRoute extends `${infer _Head}/{${infer _Param}}${infer Tail}` ? [
          pathParameter: string | number | PathParameterWithOptions,
          ...pathParameters: PathParameters<Tail>
      ] : [
      ];


      // Reachable via: Client → RawHttpHeaders
      // Reachable via: FullOperationResponse → RawHttpHeaders
      // Reachable via: HttpHeaders → RawHttpHeaders
      // Reachable via: HttpResponse → RawHttpHeaders
      // Reachable via: OperationOptions → RawHttpHeaders
      /** A HttpHeaders collection represented as a simple JSON object. */
      export type RawHttpHeaders = {
          [headerName: string]: string;
      };


      // Reachable via: Client → RawHttpHeadersInput
      // Reachable via: OperationOptions → RawHttpHeadersInput
      // Reachable via: OperationRequestOptions → RawHttpHeadersInput
      // Reachable via: RequestParameters → RawHttpHeadersInput
      /** A HttpHeaders collection for input, represented as a simple JSON object. */
      export type RawHttpHeadersInput = Record<string, string | number | boolean>;


      // Reachable via: FullOperationResponse → RequestBodyType
      // Reachable via: OperationOptions → RequestBodyType
      // Reachable via: PipelineRequest → RequestBodyType
      // Reachable via: PipelineRequestOptions → RequestBodyType
      /** Types of bodies supported on the request. */
      export type RequestBodyType = NodeReadableStream | (() => NodeReadableStream) | WebReadableStream<Uint8Array> | (() => WebReadableStream<Uint8Array>) | Blob | ArrayBuffer | ArrayBufferView | FormData | string | null;


      // Reachable via: HttpClient → SendRequest
      // Reachable via: PipelinePolicy → SendRequest
      /** A simple interface for making a pipeline request and receiving a response. */
      export type SendRequest = (request: PipelineRequest) => Promise<PipelineResponse>;


      // Reachable via: Client → TransferProgressEvent
      // Reachable via: OperationOptions → TransferProgressEvent
      // Reachable via: OperationRequestOptions → TransferProgressEvent
      // Reachable via: PipelineRequest → TransferProgressEvent
      // Reachable via: PipelineRequestOptions → TransferProgressEvent
      // Reachable via: RequestParameters → TransferProgressEvent
      /** Fired in response to upload or download progress. */
      export type TransferProgressEvent = {
          /**
           * The number of bytes loaded so far.
           */
          loadedBytes: number;
      };


      // Reachable via: PipelineRequest → HttpMethods
      // Reachable via: PipelineRequestOptions → HttpMethods
      /** Supported HTTP methods to use when making requests. */
      export type HttpMethods = "GET" | "PUT" | "POST" | "DELETE" | "PATCH" | "HEAD" | "OPTIONS" | "TRACE";


      // Reachable via: FormDataMap → FormDataValue
      /** Each form data entry can be a string, Blob, or a File. If you wish to pass a file with a name but do not have */
      export type FormDataValue = string | Blob | File;


      // Reachable via: PipelineRequest → FormDataMap
      // Reachable via: PipelineRequestOptions → FormDataMap
      /** A simple object that provides form data, as if from a browser form. */
      export type FormDataMap = {
          [key: string]: FormDataValue | FormDataValue[];
      };


      // Reachable via: LoggerContext → TypeSpecRuntimeLogLevel
      /** The log levels supported by the logger. */
      export type TypeSpecRuntimeLogLevel = "verbose" | "info" | "warning" | "error";


      // Reachable via: LoggerContext → TypeSpecRuntimeClientLogger
      /** A TypeSpecRuntimeClientLogger is a function that can log to an appropriate severity level. */
      export type TypeSpecRuntimeClientLogger = Debugger;


      // Reachable via: AddPolicyOptions → PipelinePhase
      // Reachable via: Pipeline → PipelinePhase
      /** Policies are executed in phases. */
      export type PipelinePhase = "Deserialize" | "Serialize" | "Retry" | "Sign";


      // Reachable via: BodyPart → NodeReadableStream
      // Reachable via: HttpNodeStreamResponse → NodeReadableStream
      // Reachable via: PipelineResponse → NodeReadableStream
      // Reachable via: RequestBodyType → NodeReadableStream
      /** Re-export of `NodeJS.ReadableStream` for use in platform-neutral code. */
      export type NodeReadableStream = NodeJS.ReadableStream;


      // Reachable via: KeyObject → NodeBuffer
      // Reachable via: PipelineOptions → NodeBuffer
      // Reachable via: PxfObject → NodeBuffer
      // Reachable via: TlsSettings → NodeBuffer
      /** Re-export of `Buffer` for use in platform-neutral code. */
      export type NodeBuffer = Buffer;


      // Reachable via: BodyPart → WebReadableStream
      // Reachable via: Client → WebReadableStream
      // Reachable via: HttpBrowserStreamResponse → WebReadableStream
      // Reachable via: OperationOptions → WebReadableStream
      // Reachable via: PipelineResponse → WebReadableStream
      // Reachable via: RequestBodyType → WebReadableStream
      /** Re-export of the Web `ReadableStream` for use in platform-neutral code. */
      export type WebReadableStream<R = any> = ReadableStream<R>;


      /** The supported character encoding type */
      export type EncodingType = "utf-8" | "base64" | "base64url" | "hex";


      /** A generic shape for a plain JS object. */
      export type UnknownObject = {
          [s: string]: unknown;
      };


      /** Creates a client with a default pipeline */
      export function getClient(endpoint: string, clientOptions?: ClientOptions): Client;


      /** Helper function to convert OperationOptions to RequestParameters */
      export function operationOptionsToRequestParameters(options: OperationOptions): RequestParameters;


      /** Creates a rest error from a PathUnchecked response */
      export function createRestError(response: PathUncheckedResponse): RestError;


      /** Create the correct HttpClient for the current environment. */
      export function createDefaultHttpClient(): HttpClient;


      /** Creates an object that satisfies the `HttpHeaders` interface. */
      export function createHttpHeaders(rawHeaders?: RawHttpHeadersInput): HttpHeaders;


      /** Retrieves the currently specified log level. */
      export function setLogLevel(logLevel?: TypeSpecRuntimeLogLevel): void;


      /** Retrieves the currently specified log level. */
      export function getLogLevel(): TypeSpecRuntimeLogLevel | undefined;


      /** Creates a totally empty pipeline. */
      export function createEmptyPipeline(): Pipeline;


      /** Creates a new pipeline request with the given options. */
      export function createPipelineRequest(options: PipelineRequestOptions): PipelineRequest;


      /** Typeguard for RestError */
      export function isRestError(e: unknown): e is RestError;


      /** The helper that transforms bytes with specific character encoding into string */
      export function uint8ArrayToString(bytes: Uint8Array, format: EncodingType): string;


      /** The helper that transforms string to specific character encoded bytes array. */
      export function stringToUint8Array(value: string, format: EncodingType): Uint8Array;



  // --- @typespec/ts-http-runtime/internal/logger ---

      /** todo doc */
      export interface LoggerContext {
          logger: TypeSpecRuntimeClientLogger;
          setLogLevel(logLevel?: TypeSpecRuntimeLogLevel): void;
          getLogLevel(): TypeSpecRuntimeLogLevel | undefined;
      }


      /** Option for creating a TypeSpecRuntimeLoggerContext. */
      export interface CreateLoggerContextOptions {
          logLevelEnvVarName: string;
          namespace: string;
      }


      /** Creates a logger context base on the provided options. */
      export function createLoggerContext(options: CreateLoggerContextOptions): LoggerContext;



  // --- @typespec/ts-http-runtime/internal/policies ---

      /** Options that control how to retry failed requests. */
      export interface DefaultRetryPolicyOptions extends PipelineRetryOptions {
      }


      /** Options that control how to retry failed requests. */
      export interface ExponentialRetryPolicyOptions {
          maxRetries?: number;
          retryDelayInMs?: number;
          maxRetryDelayInMs?: number;
      }


      /** /** */
      export interface RetryPolicyOptions {
          maxRetries?: number;
          logger?: TypeSpecRuntimeLogger;
      }


      // Reachable via: RetryStrategy → RetryInformation
      /** Information provided to the retry strategy about the current progress of the retry policy. */
      export interface RetryInformation {
          response?: PipelineResponse;
          responseError?: RestError;
          retryCount: number;
      }


      // Reachable via: RetryStrategy → RetryModifiers
      /** Properties that can modify the behavior of the retry policy. */
      export interface RetryModifiers {
          skipStrategy?: boolean;
          redirectTo?: string;
          retryAfterInMs?: number;
          errorToThrow?: RestError;
      }


      /** A retry strategy is intended to define whether to retry or not, and how to retry. */
      export interface RetryStrategy {
          name: string;
          logger?: TypeSpecRuntimeLogger;
          retry(state: RetryInformation): RetryModifiers;
      }


      /** Gets a pipeline policy that sets http.agent */
      export function agentPolicy(agent?: Agent): PipelinePolicy;


      /** A policy to enable response decompression according to Accept-Encoding header */
      export function decompressResponsePolicy(): PipelinePolicy;


      /** A policy that retries according to three strategies: */
      export function defaultRetryPolicy(options?: DefaultRetryPolicyOptions): PipelinePolicy;


      /** A policy that attempts to retry requests while introducing an exponentially increasing delay. */
      export function exponentialRetryPolicy(options?: ExponentialRetryPolicyOptions): PipelinePolicy;


      /** A policy that encodes FormData on the request into the body. */
      export function formDataPolicy(): PipelinePolicy;


      /** A policy that logs all requests and responses. */
      export function logPolicy(options?: LogPolicyOptions): PipelinePolicy;


      /** Pipeline policy for multipart requests */
      export function multipartPolicy(): PipelinePolicy;


      /** @deprecated - Internally this method is no longer necessary when setting proxy information. */
      /** This method converts a proxy url into `ProxySettings` for use with ProxyPolicy. */
      export function getDefaultProxySettings(proxyUrl?: string): ProxySettings | undefined;


      /** A policy that allows one to apply proxy settings to all requests. */
      export function proxyPolicy(proxySettings?: ProxySettings, options?: {
          /** a list of patterns to override those loaded from NO_PROXY environment variable. */
          customNoProxyList?: string[];
      }): PipelinePolicy;


      /** A policy to follow Location headers from the server in order */
      export function redirectPolicy(options?: RedirectPolicyOptions): PipelinePolicy;


      /** retryPolicy is a generic policy to enable retrying requests when certain conditions are met */
      export function retryPolicy(strategies: RetryStrategy[], options?: RetryPolicyOptions): PipelinePolicy;


      /** A retry policy that specifically seeks to handle errors in the */
      export function systemErrorRetryPolicy(options?: SystemErrorRetryPolicyOptions): PipelinePolicy;


      /** A policy that retries when the server sends a 429 response with a Retry-After header. */
      export function throttlingRetryPolicy(options?: ThrottlingRetryPolicyOptions): PipelinePolicy;


      /** Gets a pipeline policy that adds the client certificate to the HttpClient agent for authentication. */
      export function tlsPolicy(tlsSettings?: TlsSettings): PipelinePolicy;


      /** A policy that sets the User-Agent header (or equivalent) to reflect */
      export function userAgentPolicy(options?: UserAgentPolicyOptions): PipelinePolicy;



  // --- @typespec/ts-http-runtime/internal/util ---

      /** A utility class to sanitize objects for logging. */
      export class Sanitizer {
          constructor({ additionalAllowedHeaderNames: allowedHeaderNames, additionalAllowedQueryParameters: allowedQueryParameters, }?: SanitizerOptions);
          sanitize(obj: unknown): string;
          sanitizeUrl(value: string): string;
      }


      // Reachable via: Sanitizer → SanitizerOptions
      /** Sanitizer options */
      export interface SanitizerOptions {
          additionalAllowedHeaderNames?: string[];
          additionalAllowedQueryParameters?: string[];
      }


      /** Calculates the delay interval for retry attempts using exponential delay with jitter. */
      export function calculateRetryDelay(retryAttempt: number, config: {
          retryDelayInMs: number;
          maxRetryDelayInMs: number;
      }): {
          retryAfterInMs: number;
      };


      /** Typeguard for an error object shape (has name and message) */
      export function isError(e: unknown): e is Error;


      /** Helper to determine when an input is a generic JS object. */
      export function isObject(input: unknown): input is UnknownObject;


      /** Returns a random integer value between a lower and upper bound, */
      export function getRandomIntegerInclusive(min: number, max: number): number;


      /** Generates a SHA-256 HMAC signature. */
      export function computeSha256Hmac(key: string, stringToSign: string, encoding: "base64" | "hex"): Promise<string>;


      /** Generates a SHA-256 hash. */
      export function computeSha256Hash(content: string, encoding: "base64" | "hex"): Promise<string>;


      /** Generated Universally Unique Identifier */
      export function randomUUID(): string;



  // ... truncated (1 items omitted)

}

