import { findParametersPath, getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient } from "../../xlc/apiVersion/utils";

import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import path from "path";
import shell from "shelljs";

const findRestClientPath = (packageRoot: string): string => {
    const restPath = path.join(packageRoot, "src/");
    const fileNames = shell.ls(restPath);
    const clientFiles = fileNames.filter((f) => f.endsWith("Client.ts"));
    if (clientFiles.length !== 1)
        throw new Error(`Single client is supported, but found "${clientFiles}" in ${restPath}`);

    const clientPath = path.join(restPath, clientFiles[0]);
    return clientPath;
};

export const getApiVersionType: IApiVersionTypeExtractor = (
    packageRoot: string
): ApiVersionType => {
    const relativeRestSrcFolder = "src/";
    const typeFromClient = getApiVersionTypeFromRestClient(packageRoot, relativeRestSrcFolder, findRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot, relativeRestSrcFolder, findParametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
