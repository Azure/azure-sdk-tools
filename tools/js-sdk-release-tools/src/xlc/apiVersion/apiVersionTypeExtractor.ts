import { getSDKType } from "../../common/utils";
import { ApiVersionType, SDKType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import * as mlcApi from '../../mlc/apiVersion/apiVersionTypeExtractor'
import * as hlcApi from '../../hlc/apiVersion/apiVersionTypeExtractor'

export const getApiVersionType: IApiVersionTypeExtractor = (packageRoot: string): ApiVersionType => {
    const sdkType = getSDKType(packageRoot);
    switch (sdkType) {
        case SDKType.ModularClient:
            return mlcApi.getApiVersionType(packageRoot);
        case SDKType.HighLevelClient:
            return hlcApi.getApiVersionType(packageRoot);
        default:
            console.warn(`Unsupported SDK type ${sdkType} to get detact api version`);
            return ApiVersionType.None;
    }
}
