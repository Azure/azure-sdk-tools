/**
 * gh-client.ts — the injectable request layer for the raw-fetch path.
 *
 * `fetchPrToCache` (fetch-prs.ts) and both PR assemblers issue every GitHub read
 * through a {@link GhClient} instead of calling the `utils.ts` helpers directly.
 * This is the seam Phase 2 needs: production uses {@link defaultGhClient} (the
 * real async, rate-limited helpers), while tests substitute an in-process
 * {@link RecordingClient} (or fixture client) to observe call count / args /
 * concurrency and to drive REST↔GraphQL equivalence — without spawning a real
 * `gh`. Phase 4 reuses the same recorder for request-count deltas.
 *
 * Both methods return already-flattened JSON (matching `--paginate --slurp`
 * semantics of the underlying helpers), so callers see a single object/array.
 */
import { ghApiGraphqlAsync, ghApiJsonAsync } from "./utils.ts";

/** Minimal GitHub read surface the raw-fetch path depends on. */
export interface GhClient {
    /** REST GET of `endpoint` (fully paginated + slurp-flattened). */
    restJson<T>(endpoint: string): Promise<T>;
    /** `gh api graphql` with the given `-f/-F` fields; returns the lone object. */
    graphql<T>(fields: string[]): Promise<T>;
}

/** Production client: the real rate-limited async helpers from utils.ts. */
export const defaultGhClient: GhClient = {
    restJson: <T>(endpoint: string): Promise<T> => ghApiJsonAsync<T>(endpoint),
    graphql: <T>(fields: string[]): Promise<T> => ghApiGraphqlAsync<T>(fields),
};

/** One recorded request: `rest <endpoint>` or `graphql` (with its fields). */
export interface RecordedCall {
    kind: "rest" | "graphql";
    /** REST endpoint, or `"graphql"` for GraphQL calls. */
    endpoint: string;
    /** GraphQL `-f/-F` fields (empty for REST). */
    fields: string[];
}

/** In-process {@link GhClient} that records every call and delegates responses. */
export interface RecordingClient extends GhClient {
    /** All calls in issue order. */
    readonly calls: RecordedCall[];
    /** REST endpoints only, in order. */
    restEndpoints(): string[];
    /** Count of GraphQL calls. */
    graphqlCount(): number;
    /** Highest number of simultaneously in-flight calls observed. */
    peakConcurrency(): number;
}

/**
 * Wrap a backing {@link GhClient} (default: a not-implemented stub) so every call
 * is recorded. Pass `rest`/`graphql` responders to serve fixtures in tests. Both
 * responders may be async; concurrency is tracked across the awaited window.
 */
export function makeRecordingClient(responders?: {
    rest?: <T>(endpoint: string) => Promise<T> | T;
    graphql?: <T>(fields: string[]) => Promise<T> | T;
}): RecordingClient {
    const calls: RecordedCall[] = [];
    let active = 0;
    let peak = 0;

    async function track<T>(fn: () => Promise<T> | T): Promise<T> {
        active++;
        peak = Math.max(peak, active);
        try {
            return await fn();
        } finally {
            active--;
        }
    }

    const restResponder =
        responders?.rest ??
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract for callers
        (<T>(endpoint: string): T => {
            throw new Error(
                `RecordingClient: no rest responder for ${endpoint}`,
            );
        });
    const graphqlResponder =
        responders?.graphql ??
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract for callers
        (<T>(): T => {
            throw new Error("RecordingClient: no graphql responder");
        });

    return {
        calls,
        restEndpoints: () =>
            calls.filter((c) => c.kind === "rest").map((c) => c.endpoint),
        graphqlCount: () => calls.filter((c) => c.kind === "graphql").length,
        peakConcurrency: () => peak,
        restJson: <T>(endpoint: string): Promise<T> => {
            calls.push({ kind: "rest", endpoint, fields: [] });
            return track<T>(() => restResponder<T>(endpoint));
        },
        graphql: <T>(fields: string[]): Promise<T> => {
            calls.push({ kind: "graphql", endpoint: "graphql", fields });
            return track<T>(() => graphqlResponder<T>(fields));
        },
    };
}
