// subpaths.d.ts — file with declare module blocks creating multiple subpath exports.

declare module "." {
  export { BlobClient } from "./storage";
  export { StorageOptions } from "./models";

  /**
   * Creates a BlobClient with the given connection string.
   */
  export declare function createBlobClient(connectionString: string): BlobClient;
}

declare module "./models" {
  /**
   * Options for storage operations.
   */
  export interface StorageOptions {
    timeout?: number;
    retries?: number;
  }

  /**
   * Known storage error codes.
   */
  export declare enum StorageErrorCode {
    NotFound = "NotFound",
    Conflict = "Conflict",
    Unauthorized = "Unauthorized",
  }
}

declare module "./storage" {
  import { StorageOptions } from "./models";

  /**
   * Client for Azure Blob Storage.
   */
  export declare class BlobClient {
    constructor(url: string, options?: StorageOptions);
    readonly url: string;
    upload(data: Uint8Array): Promise<void>;
    download(): Promise<Uint8Array>;
    delete(): Promise<void>;
  }
}
