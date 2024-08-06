import { Client } from "@azure-rest/core-client";
import { AbortSignalLike } from "@azure/abort-controller";
import { CancelOnProgress, CreateHttpPollerOptions, OperationState } from "@azure/core-lro";
import { DataTypesCreate200Response, DataTypesCreate201Response, DataTypesCreateDefaultResponse, DataTypesCreateLogicalResponse, DataTypesUpdate200Response, DataTypesUpdate202Response, DataTypesUpdateDefaultResponse, DataTypesUpdateLogicalResponse, DataTypesDelete202Response, DataTypesDelete204Response, DataTypesDeleteDefaultResponse, DataTypesDeleteLogicalResponse, DataTypesDeleteData202Response, DataTypesDeleteData204Response, DataTypesDeleteDataDefaultResponse, DataTypesDeleteDataLogicalResponse, DataProductsCreate200Response, DataProductsCreate201Response, DataProductsCreateDefaultResponse, DataProductsCreateLogicalResponse, DataProductsUpdate200Response, DataProductsUpdate202Response, DataProductsUpdateDefaultResponse, DataProductsUpdateLogicalResponse, DataProductsDelete202Response, DataProductsDelete204Response, DataProductsDeleteDefaultResponse, DataProductsDeleteLogicalResponse } from "./responses.js";
/**
 * A simple poller that can be used to poll a long running operation.
 */
export interface SimplePollerLike<TState extends OperationState<TResult>, TResult> {
    /**
     * Returns true if the poller has finished polling.
     */
    isDone(): boolean;
    /**
     * Returns the state of the operation.
     */
    getOperationState(): TState;
    /**
     * Returns the result value of the operation,
     * regardless of the state of the poller.
     * It can return undefined or an incomplete form of the final TResult value
     * depending on the implementation.
     */
    getResult(): TResult | undefined;
    /**
     * Returns a promise that will resolve once a single polling request finishes.
     * It does this by calling the update method of the Poller's operation.
     */
    poll(options?: {
        abortSignal?: AbortSignalLike;
    }): Promise<TState>;
    /**
     * Returns a promise that will resolve once the underlying operation is completed.
     */
    pollUntilDone(pollOptions?: {
        abortSignal?: AbortSignalLike;
    }): Promise<TResult>;
    /**
     * Invokes the provided callback after each polling is completed,
     * sending the current state of the poller's operation.
     *
     * It returns a method that can be used to stop receiving updates on the given callback function.
     */
    onProgress(callback: (state: TState) => void): CancelOnProgress;
    /**
     * Returns a promise that could be used for serialized version of the poller's operation
     * by invoking the operation's serialize method.
     */
    serialize(): Promise<string>;
    /**
     * Wait the poller to be submitted.
     */
    submitted(): Promise<void>;
    /**
     * Returns a string representation of the poller's operation. Similar to serialize but returns a string.
     * @deprecated Use serialize() instead.
     */
    toString(): string;
    /**
     * Stops the poller from continuing to poll. Please note this will only stop the client-side polling
     * @deprecated Use abortSignal to stop polling instead.
     */
    stopPolling(): void;
    /**
     * Returns true if the poller is stopped.
     * @deprecated Use abortSignal status to track this instead.
     */
    isStopped(): boolean;
}
/**
 * Helper function that builds a Poller object to help polling a long running operation.
 * @param client - Client to use for sending the request to get additional pages.
 * @param initialResponse - The initial response.
 * @param options - Options to set a resume state or custom polling interval.
 * @returns - A poller object to poll for operation state updates and eventually get the final response.
 */
export declare function getLongRunningPoller<TResult extends DataTypesCreateLogicalResponse | DataTypesCreateDefaultResponse>(client: Client, initialResponse: DataTypesCreate200Response | DataTypesCreate201Response | DataTypesCreateDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
export declare function getLongRunningPoller<TResult extends DataTypesUpdateLogicalResponse | DataTypesUpdateDefaultResponse>(client: Client, initialResponse: DataTypesUpdate200Response | DataTypesUpdate202Response | DataTypesUpdateDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
export declare function getLongRunningPoller<TResult extends DataTypesDeleteLogicalResponse | DataTypesDeleteDefaultResponse>(client: Client, initialResponse: DataTypesDelete202Response | DataTypesDelete204Response | DataTypesDeleteDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
export declare function getLongRunningPoller<TResult extends DataTypesDeleteDataLogicalResponse | DataTypesDeleteDataDefaultResponse>(client: Client, initialResponse: DataTypesDeleteData202Response | DataTypesDeleteData204Response | DataTypesDeleteDataDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
export declare function getLongRunningPoller<TResult extends DataProductsCreateLogicalResponse | DataProductsCreateDefaultResponse>(client: Client, initialResponse: DataProductsCreate200Response | DataProductsCreate201Response | DataProductsCreateDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
export declare function getLongRunningPoller<TResult extends DataProductsUpdateLogicalResponse | DataProductsUpdateDefaultResponse>(client: Client, initialResponse: DataProductsUpdate200Response | DataProductsUpdate202Response | DataProductsUpdateDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
export declare function getLongRunningPoller<TResult extends DataProductsDeleteLogicalResponse | DataProductsDeleteDefaultResponse>(client: Client, initialResponse: DataProductsDelete202Response | DataProductsDelete204Response | DataProductsDeleteDefaultResponse, options?: CreateHttpPollerOptions<TResult, OperationState<TResult>>): Promise<SimplePollerLike<OperationState<TResult>, TResult>>;
//# sourceMappingURL=pollingHelper.d.ts.map