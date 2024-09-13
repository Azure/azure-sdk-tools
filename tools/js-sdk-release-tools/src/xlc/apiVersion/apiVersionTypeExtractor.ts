import { getSDKType } from "../../common/utils";
import { ApiVersionType, SDKType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import * as mlcApi from '../../mlc/apiVersion/apiVersionTypeExtractor'
import * as hlcApi from '../../hlc/apiVersion/apiVersionTypeExtractor'
import * as rlcApi from '../../llc/apiVersion/apiVersionTypeExtractor'

export const getApiVersionType: IApiVersionTypeExtractor = async (packageRoot: string): Promise<ApiVersionType> => {
    const sdkType = getSDKType(packageRoot);
    switch (sdkType) {
        case SDKType.ModularClient:
            return await mlcApi.getApiVersionType(packageRoot);
        case SDKType.HighLevelClient:
            return await hlcApi.getApiVersionType(packageRoot);
        case SDKType.RestLevelClient:
            return await rlcApi.getApiVersionType(packageRoot); 
        default:
            console.warn(`Unsupported SDK type ${sdkType} to get detact api version`);
            return ApiVersionType.None;
    }
}
