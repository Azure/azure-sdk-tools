// tags.d.ts — declarations with @beta, @alpha, and @deprecated TSDoc tags.

/**
 * A stable public interface.
 */
export interface StableOptions {
  timeout: number;
}

/**
 * A beta interface — under active development.
 * @beta
 */
export interface BetaFeatureOptions {
  experimentalRetry?: boolean;
}

/**
 * An alpha interface — may change radically.
 * @alpha
 */
export interface AlphaFeatureOptions {
  previewMode: boolean;
}

/**
 * @deprecated Use StableClient instead.
 */
export declare class LegacyClient {
  send(): void;
}

/**
 * A stable class whose child members carry their own tags.
 */
export declare class MixedClient {
  /**
   * Stable method — always available.
   */
  stableMethod(): void;

  /**
   * Beta method on a stable class.
   * @beta
   */
  betaMethod(): Promise<void>;

  /**
   * @deprecated Use betaMethod() instead.
   */
  oldMethod(): void;
}

/**
 * Beta namespace.
 * @beta
 */
export declare namespace BetaUtils {
  /**
   * A method inside a beta namespace.
   * Beta tag should NOT be re-emitted here (parent already carries @beta).
   */
  function helper(): void;

  /**
   * An alpha method nested inside a beta namespace.
   * @alpha — different tag; should still be emitted.
   */
  function experimental(): void;
}

/**
 * @beta
 */
export declare function betaFunction(x: string): number;

/**
 * @alpha
 */
export declare const ALPHA_CONSTANT: string;
