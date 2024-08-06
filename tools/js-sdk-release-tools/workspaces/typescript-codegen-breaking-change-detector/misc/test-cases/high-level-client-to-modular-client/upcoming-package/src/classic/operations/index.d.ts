import { NetworkAnalyticsContext } from "../../api/networkAnalyticsContext.js";
import { Operation } from "../../models/models.js";
import { PagedAsyncIterableIterator } from "../../models/pagingTypes.js";
import { OperationsListOptionalParams } from "../../models/options.js";
export interface OperationsOperations {
    list: (options?: OperationsListOptionalParams) => PagedAsyncIterableIterator<Operation>;
}
export declare function getOperations(context: NetworkAnalyticsContext): {
    list: (options?: OperationsListOptionalParams) => PagedAsyncIterableIterator<Operation, Operation[], import("../../models/pagingTypes.js").PageSettings>;
};
export declare function getOperationsOperations(context: NetworkAnalyticsContext): OperationsOperations;
//# sourceMappingURL=index.d.ts.map